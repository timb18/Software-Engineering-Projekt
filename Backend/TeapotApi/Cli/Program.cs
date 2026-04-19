using Microsoft.Extensions.DependencyInjection;
using Services;

var services = new ServiceCollection();
services.AddTeapotServices();

using ServiceProvider provider = services.BuildServiceProvider();

if (args.Length == 0) // If no arguments exist query
{
    PrintUsage();
    return 1;
}

string command = args[0];
if (!string.Equals(command, "create-org", StringComparison.OrdinalIgnoreCase)) // If the command is not equal 'create-org'
{
    Console.Error.WriteLine($"Unknown command: {command}");
    PrintUsage();
    return 1;
}

Dictionary<string, string> options;
try
{
    options = ParseOptions(args.Skip(1)); // Try parsing options
}
catch (ArgumentException ex)
{
    Console.Error.WriteLine(ex.Message);
    PrintUsage();
    return 1;
}

if (!TryReadRequiredOption(options, "name", out string organizationName)
    || !TryReadRequiredOption(options, "description", out string organizationDescription)
    || !TryReadRequiredOption(options, "invitation-quota", out string invitationQuotaRaw)
    || !TryReadRequiredOption(options, "organizer-username", out string organizerUserName)
    || !TryReadRequiredOption(options, "organizer-email", out string organizerEmail)) // If TryReadRequiredOption() fails (checks for all options)
{
    PrintUsage();
    return 1;
}

if (!int.TryParse(invitationQuotaRaw, out int invitationQuota))
{
    Console.Error.WriteLine("--invitation-quota must be an integer.");
    return 1;
}

IOrganizationAdminService adminService = provider.GetRequiredService<IOrganizationAdminService>();

// Try creating new Organisation with all needed parameters
try
{
    CreateOrganizationResult result = await adminService.CreateOrganizationAsync(new CreateOrganizationRequest 
    {
        OrganizationName = organizationName,
        OrganizationDescription = organizationDescription,
        InvitationQuota = invitationQuota,
        OrganizerUserName = organizerUserName,
        OrganizerEmail = organizerEmail
    });

    // Logging
    Console.WriteLine("Organization created successfully."); 
    Console.WriteLine($"OrganizationId: {result.OrganizationId}");
    Console.WriteLine($"AdminUserId: {result.OrganizerUserId}");
    return 0;
}
catch (Exception ex)
{
    Console.Error.WriteLine($"Failed to create organization: {ex.Message}");
    return 2;
}

// Function for parsing the options from the imputcommand
static Dictionary<string, string> ParseOptions(IEnumerable<string> optionArgs)
{
    var options = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    string? pendingKey = null; // Remembers last key

    // Loop over every given argument in the command
    foreach (string token in optionArgs)
    {
        if (token.StartsWith("--", StringComparison.Ordinal)) // If token starts with '--' (option)
        {
            if (pendingKey is not null) // pendingKey != null -> called option without value
            {
                throw new ArgumentException($"Missing value for --{pendingKey}");
            }

            pendingKey = token[2..]; // Deletes first two chars (--)
            if (string.IsNullOrWhiteSpace(pendingKey)) // If no option is selected
            {
                throw new ArgumentException("Invalid option name.");
            }

            continue;
        }

        if (pendingKey is null)
        {
            throw new ArgumentException($"Unexpected token: {token}");
        }

        options[pendingKey] = token; // Assigns token to pendingKey from dictionary
        pendingKey = null; // Resets pendingKey
    }

    if (pendingKey is not null)
    {
        throw new ArgumentException($"Missing value for --{pendingKey}");
    }

    return options;
}

// Function checks if the key is not in the dicionary or if the value is empty and the throws an error
static bool TryReadRequiredOption(
    IReadOnlyDictionary<string, string> options,
    string key,
    out string value)
{
    if (!options.TryGetValue(key, out string? rawValue) || string.IsNullOrWhiteSpace(rawValue))
    {
        Console.Error.WriteLine($"Missing required option: --{key}");
        value = string.Empty;
        return false;
    }

    value = rawValue;
    return true;
}

// Print usage of the 'create-org'-command
static void PrintUsage()
{
    Console.WriteLine("Usage:");
    Console.WriteLine("  create-org --name <organizationName> --description <description> --invitation-quota <number> --organizer-username <userName> --organizer-email <email>");
}
