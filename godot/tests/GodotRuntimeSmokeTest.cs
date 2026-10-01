using Chickensoft.GoDotTest;
using Godot;
using Shouldly;

namespace RollForHonor.Godot.Tests;

public sealed class GodotRuntimeSmokeTest(Node testScene) : TestClass(testScene)
{
    [Test]
    public void TestSceneIsInsideSceneTree()
    {
        TestScene.GetTree().ShouldNotBeNull();
    }

    [Test]
    public void CanCreateNode3D()
    {
        Node3D node = new();

        TestScene.AddChild(node);

        node.IsInsideTree().ShouldBeTrue();
        node.QueueFree();
    }
}