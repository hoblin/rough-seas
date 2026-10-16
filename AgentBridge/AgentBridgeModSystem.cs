using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;

namespace AgentBridge;

public class AgentBridgeModSystem : ModSystem
{
    private static readonly string Prefix = $"http://127.0.0.1:{Environment.GetEnvironmentVariable("AGENT_BRIDGE_PORT") ?? "42425"}/";
    // A browser cannot add a custom header to a cross-origin request without a preflight, which the bridge never answers.
    private const string Header = "X-Agent-Bridge";
    private static readonly string[] Vitals = ["health", "oxygen", "bodyTemp", "wetness"];

    private ICoreClientAPI capi = null!;
    private ScreenshotCapture screenshots = null!;
    private HoverOverride hover = null!;
    private EntityLoadBlackBox blackBox = null!;
    private readonly HttpListener listener = new();

    public override bool ShouldLoad(EnumAppSide forSide) => forSide == EnumAppSide.Client;

    public override void StartClientSide(ICoreClientAPI api)
    {
        capi = api;
        screenshots = new ScreenshotCapture(api);
        hover = new HoverOverride(api);
        blackBox = new EntityLoadBlackBox(Mod.Logger);
        listener.Prefixes.Add(Prefix);
        listener.Start();
        _ = Task.Run(Serve);
        Mod.Logger.Notification($"Agent bridge listening on {Prefix}");
    }

    public override void Dispose()
    {
        listener.Close();
        screenshots?.Dispose();
        hover?.Dispose();
        blackBox?.Dispose();
    }

    private async Task Serve()
    {
        while (listener.IsListening)
        {
            HttpListenerContext context;
            try
            {
                context = await listener.GetContextAsync();
            }
            catch (Exception) when (!listener.IsListening)
            {
                return;
            }
            _ = Task.Run(() => Handle(context));
        }
    }

    private async Task Handle(HttpListenerContext context)
    {
        var response = context.Response;
        try
        {
            if (context.Request.Headers[Header] == null) throw new UnauthorizedAccessException($"Send the {Header} header, as bin/game does");

            var (body, contentType) = context.Request.Url!.AbsolutePath switch
            {
                "/chat" => (Encoding.UTF8.GetBytes(await Chat(context.Request)), "text/plain"),
                "/state" => (Encoding.UTF8.GetBytes(await OnMainThread(State)), "application/json"),
                "/screenshot" => (await screenshots.Next(), "image/png"),
                "/close-dialogs" => (Encoding.UTF8.GetBytes(await OnMainThread(CloseDialogs)), "text/plain"),
                "/look" => (Encoding.UTF8.GetBytes(await OnMainThread(() => Look(context.Request))), "application/json"),
                "/input" => (Encoding.UTF8.GetBytes(await Input(context.Request)), "application/json"),
                "/hover" => (Encoding.UTF8.GetBytes(await OnMainThread(() => Hover(context.Request.QueryString["entity"], context.Request.QueryString["block"]))), "application/json"),
                "/hotkey" =>(Encoding.UTF8.GetBytes(await OnMainThread(() => Hotkey(context.Request.QueryString["code"]))), "text/plain"),
                _ => throw new FileNotFoundException("Unknown endpoint; bin/game help lists what the bridge does")
            };
            response.ContentType = contentType;
            await response.OutputStream.WriteAsync(body);
        }
        catch (Exception e)
        {
            response.StatusCode = e switch
            {
                UnauthorizedAccessException => 403,
                FileNotFoundException => 404,
                _ => 500
            };
            await response.OutputStream.WriteAsync(Encoding.UTF8.GetBytes(e.Message + "\n"));
        }
        finally
        {
            response.Close();
        }
    }

    private async Task<string> Chat(HttpListenerRequest request)
    {
        var line = (await new StreamReader(request.InputStream).ReadToEndAsync()).Trim();
        var wait = int.TryParse(request.QueryString["wait"], out var ms) ? ms : 1000;
        var replies = new List<string>();
        ChatLineDelegate collect = (_, message, _, _) => replies.Add(message);

        await OnMainThread(() =>
        {
            capi.Event.ChatMessage += collect;
            if (line.StartsWith('.')) capi.TriggerChatMessage(line);
            else capi.SendChatMessage(line);
            return true;
        });
        await Task.Delay(wait);
        return await OnMainThread(() =>
        {
            capi.Event.ChatMessage -= collect;
            return string.Join("\n", replies) + "\n";
        });
    }

    private string State()
    {
        var entity = capi.World.Player.Entity;
        var mount = entity.MountedOn?.MountSupplier?.OnEntity;
        return JsonSerializer.Serialize(new
        {
            pos = new { entity.Pos.X, entity.Pos.Y, entity.Pos.Z },
            camera = new { yaw = capi.World.Player.CameraYaw, pitch = capi.World.Player.CameraPitch },
            swimming = entity.Swimming,
            vitals = Vitals.ToDictionary(key => key, key => JsonNode.Parse(entity.WatchedAttributes[key]?.ToJsonToken() ?? "null")),
            hotbar = new
            {
                slot = capi.World.Player.InventoryManager.ActiveHotbarSlotNumber,
                item = capi.World.Player.InventoryManager.ActiveHotbarSlot?.Itemstack?.Collectible.Code.ToString()
            },
            mountedOn = mount == null ? null : new { mount.EntityId, code = mount.Code.ToString() },
            lookingAt = capi.World.Player.CurrentEntitySelection?.Entity.Code.ToString()
                ?? capi.World.Player.CurrentBlockSelection?.Block?.Code.ToString(),
            reach = Reach(),
            nearby = capi.World.GetEntitiesAround(entity.Pos.XYZ, 16, 16, other => other != entity)
                .Select(other => new { other.EntityId, code = other.Code.ToString(), other.Pos.X, other.Pos.Y, other.Pos.Z }),
            calendar = capi.World.Calendar.PrettyDate(),
            paused = capi.IsGamePaused,
            dialogs = Dialogs().Select(dialog => dialog.DebugName)
        }) + "\n";
    }

    private object? Reach()
    {
        var player = capi.World.Player;
        if (player.CurrentEntitySelection is not { } selection) return null;

        var entity = player.Entity;
        var target = selection.Entity;
        var stack = player.InventoryManager.ActiveHotbarSlot?.Itemstack;
        var eye = entity.Pos.XYZ.Add(entity.LocalEyePos);
        double distance = target.SelectionBox.ToDouble().Translate(target.Pos.X, target.Pos.Y, target.Pos.Z)
            .ShortestDistanceFrom(eye.X, eye.Y, eye.Z);
        float range = stack?.Collectible.GetAttackRange(stack) ?? GlobalConstants.DefaultAttackRange;
        return new { selection.SelectionBoxIndex, distance = Math.Round(distance, 2), range, hits = distance <= range };
    }

    private string Look(HttpListenerRequest request)
    {
        var player = capi.World.Player;
        if (float.TryParse(request.QueryString["yaw"], CultureInfo.InvariantCulture, out var yaw)) player.CameraYaw = yaw;
        if (float.TryParse(request.QueryString["pitch"], CultureInfo.InvariantCulture, out var pitch)) player.CameraPitch = pitch;
        if (int.TryParse(request.QueryString["slot"], out var slot)) player.InventoryManager.ActiveHotbarSlotNumber = slot;
        if (long.TryParse(request.QueryString["entity"], out var id))
        {
            var target = capi.World.GetEntityById(id) ?? throw new FileNotFoundException($"No entity {id} near the player");
            var eye = player.Entity.Pos.XYZ.Add(player.Entity.LocalEyePos);
            var center = target.Pos.XYZ.Add(0, target.SelectionBox.Y2 / 2, 0);
            var dx = center.X - eye.X;
            var dy = center.Y - eye.Y;
            var dz = center.Z - eye.Z;
            player.CameraYaw = (float)Math.Atan2(dx, dz);
            player.CameraPitch = (float)(Math.PI - Math.Atan2(dy, Math.Sqrt(dx * dx + dz * dz)));
        }
        return State();
    }

    private string Hover(string? entity, string? block)
    {
        hover.Block = null;
        if (block?.Split(',').Select(int.Parse).ToArray() is [var x, var y, var z])
        {
            hover.Target = null;
            hover.Block = new BlockPos(x, y, z);
            return JsonSerializer.Serialize(new { block = capi.World.BlockAccessor.GetBlock(hover.Block).Code.ToString() }) + "\n";
        }
        if (!long.TryParse(entity, out var id))
        {
            hover.Target = null;
            return "null\n";
        }

        var target = capi.World.GetEntityById(id) ?? throw new FileNotFoundException($"No entity {id} near the player");
        hover.Target = target;
        return JsonSerializer.Serialize(new { name = target.GetName(), info = target.GetInfoText() }) + "\n";
    }

    private async Task<string> Input(HttpListenerRequest request)
    {
        var keys = (request.QueryString["keys"] ?? "")
            .Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(key => (int)Enum.Parse<GlKeys>(key, true))
            .ToList();
        var mouse = request.QueryString["mouse"];
        var hold = int.TryParse(request.QueryString["ms"], out var ms) ? ms : 100;

        await OnMainThread(() => Press(keys, mouse, true));
        try
        {
            await Task.Delay(hold);
        }
        finally
        {
            await OnMainThread(() => Press(keys, mouse, false));
        }
        return await OnMainThread(State);
    }

    private bool Press(List<int> keys, string? mouse, bool down)
    {
        foreach (var key in keys) capi.Input.KeyboardKeyState[key] = down;
        var buttons = capi.Input.InWorldMouseButton;
        switch (mouse)
        {
            case "left": buttons.Left = down; break;
            case "right": buttons.Right = down; break;
            case "middle": buttons.Middle = down; break;
        }
        return down;
    }

    private string Hotkey(string? code)
    {
        var hotkey = code == null ? null : capi.Input.GetHotKeyByCode(code);
        if (hotkey?.Handler == null)
        {
            return "Hotkey codes:\n" + string.Join("\n", capi.Input.HotKeys.Values.Select(key => $"{key.Code}  {key.Name}")) + "\n";
        }
        return hotkey.Handler(hotkey.CurrentMapping) + "\n";
    }

    private string CloseDialogs()
    {
        var closed = Dialogs().Where(dialog => dialog.TryClose()).Select(dialog => dialog.DebugName).ToList();
        return string.Join("\n", closed) + "\n";
    }

    private IEnumerable<GuiDialog> Dialogs() =>
        capi.Gui.OpenedGuis.Where(dialog => dialog.DialogType == EnumDialogType.Dialog).ToList();

    private Task<T> OnMainThread<T>(Func<T> action)
    {
        var result = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        capi.Event.EnqueueMainThreadTask(() =>
        {
            try
            {
                result.SetResult(action());
            }
            catch (Exception e)
            {
                result.SetException(e);
            }
        }, "agentbridge");
        return result.Task;
    }
}
