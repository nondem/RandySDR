using RtlSdrManager;

// Test to discover RtlSdrManager API
var manager = RtlSdrDeviceManager.Instance;

// Check properties
Console.WriteLine($"Manager type: {manager.GetType().FullName}");
Console.WriteLine($"Manager properties:");
foreach (var prop in manager.GetType().GetProperties())
{
    Console.WriteLine($"  - {prop.Name}: {prop.PropertyType.Name}");
}

// Check RtlSdrManagedDevice
var deviceType = typeof(RtlSdrManagedDevice);
Console.WriteLine($"\nRtlSdrManagedDevice constructors:");
foreach (var ctor in deviceType.GetConstructors())
{
    Console.WriteLine($"  - {ctor}");
}

Console.WriteLine($"\nRtlSdrManagedDevice properties:");
foreach (var prop in deviceType.GetProperties())
{
    Console.WriteLine($"  - {prop.Name}: {prop.PropertyType.Name}");
}
