using System.Runtime.CompilerServices;

// Low-level impact tests may construct contexts without exercising launch acceptance.
[assembly: InternalsVisibleTo("RollForHonor.Application.Tests")]
