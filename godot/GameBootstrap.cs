using System.Reflection;
using Chickensoft.GoDotTest;
using Godot;

namespace RollForHonor.Godot;

public partial class GameBootstrap : Node3D
{
#if DEBUG
    private TestEnvironment _testEnvironment;
#endif

    public override void _Ready()
    {
#if DEBUG
        _testEnvironment = TestEnvironment.From(OS.GetCmdlineArgs());

        if (_testEnvironment.ShouldRunTests)
        {
            CallDeferred(MethodName.RunTests);
            return;
        }
#endif

        if (ResourceLoader.Exists("res://Game.tscn"))
        {
            GetTree().ChangeSceneToFile("res://Game.tscn");
        }
    }

#if DEBUG
    private void RunTests()
    {
        _ = GoTest.RunTests(
            Assembly.GetExecutingAssembly(),
            this,
            _testEnvironment
        );
    }
#endif
}