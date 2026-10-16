import math

from mathutils import Euler, Quaternion

armature = next(obj for obj in bpy.data.objects if obj.type == "ARMATURE")
action = bpy.data.actions.new("wave")
action.use_fake_user = True
armature.animation_data_create().action = action
bones = armature.pose.bones

bones["Forearm"].rotation_mode = "QUATERNION"
bones["Fin"].rotation_mode = "XYZ"
for frame, angle in ((0, 0), (8, 50), (16, -30), (24, 0)):
    bones["Forearm"].rotation_quaternion = Quaternion((0.3, 1, 0.2), math.radians(angle))
    bones["Forearm"].location = (0, angle / 25, 0)
    bones["Forearm"].keyframe_insert("rotation_quaternion", frame=frame)
    bones["Forearm"].keyframe_insert("location", frame=frame)
    bones["Fin"].rotation_euler = Euler((math.radians(angle / 2), math.radians(angle), 0), "XYZ")
    bones["Fin"].keyframe_insert("rotation_euler", frame=frame)
    bones["Arm"].scale = (1 + angle / 200,) * 3
    bones["Arm"].keyframe_insert("scale", frame=frame)

spin = bpy.data.actions.new("spin")
spin.use_fake_user = True
armature.animation_data.action = spin
bones["Fin"].rotation_mode = "XZY"
for frame, angle in ((0, 0), (10, 180), (20, 360), (24, 360)):
    bones["Fin"].rotation_euler = Euler((0, 0, math.radians(angle)), "XZY")
    bones["Fin"].keyframe_insert("rotation_euler", frame=frame)
for frame, lift in ((0, 0), (22, 2), (24, 0)):
    bones["Forearm"].location = (0, 0, lift)
    bones["Forearm"].keyframe_insert("location", frame=frame)

vanish = bpy.data.actions.new("vanish")
vanish.use_fake_user = True
armature.animation_data.action = vanish
for frame, size in ((0, 1), (6, 0), (12, 1)):
    bones["Arm"].scale = (size,) * 3
    bones["Arm"].location = (0, 0, frame / 6)
    bones["Arm"].keyframe_insert("scale", frame=frame)
    bones["Arm"].keyframe_insert("location", frame=frame)

bpy.data.actions["nod"].name = "bow"

swing = bpy.data.actions["swing"]
for curve in swing.fcurves:
    if curve.data_path == 'pose.bones["Arm"].rotation_euler' and curve.array_index == 0:
        curve.keyframe_points[1].co.y += math.radians(20)
        curve.update()
