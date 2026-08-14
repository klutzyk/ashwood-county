param(
    [string]$GodotPath = "C:\Users\kalz9\Downloads\Godot_v4.7.1-stable_mono_win64\Godot_v4.7.1-stable_mono_win64\Godot_v4.7.1-stable_mono_win64.exe"
)

$projectRoot = Split-Path -Parent $PSScriptRoot
& $GodotPath --path $projectRoot res://scenes/tools/AuthoringStudio.tscn
