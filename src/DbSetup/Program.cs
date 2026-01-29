using Npgsql;

var connectionString = args.Length > 0 
    ? args[0] 
    : "Host=ep-royal-fire-ahxc0v77-pooler.c-3.us-east-1.aws.neon.tech;Port=5432;Database=PadelScoreDB;Username=neondb_owner;Password=npg_ETbmV1DeuLI0;SSL Mode=Require";

var projectRoot = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..");
var schemaPath = Path.Combine(projectRoot, "db", "schema.sql");

if (!File.Exists(schemaPath))
{
    Console.WriteLine($"Schema file not found at: {schemaPath}");
    return 1;
}

var sql = File.ReadAllText(schemaPath);

Console.WriteLine("Connecting to Neon DB...");
try
{
    await using var conn = new NpgsqlConnection(connectionString);
    await conn.OpenAsync();

    Console.WriteLine("Connected successfully!");
    Console.WriteLine("Executing schema...");
    
    await using var cmd = new NpgsqlCommand(sql, conn);
    await cmd.ExecuteNonQueryAsync();

    Console.WriteLine("✓ Schema executed successfully!");
    return 0;
}
catch (Exception ex)
{
    Console.WriteLine($"✗ Error: {ex.Message}");
    return 1;
}
