using Microsoft.Extensions.DependencyInjection;

namespace Cli;
using Services;
public class CommandParser(IServiceProvider provider)
{
    public async Task<int> ParseCommand(string[] args)
    {
        if (args.Length == 0) // If no arguments exist query
        {
            PrintUsage();
            throw new Exception("Missing command line arguments.");
        }

        var command = args[0];
        if (!string.Equals(command, "create-org",
                StringComparison.OrdinalIgnoreCase)) // If the command is not equal 'create-org'
        {
            await Console.Error.WriteLineAsync($"Unknown command: {command}");
            PrintUsage();
            throw new Exception($"Unknown command: {command}");
        }

        Dictionary<string, string> options;
        try
        {
            options = ParseOptions(args.Skip(1)); // Try parsing options
        }
        catch (ArgumentException ex)
        {
            await Console.Error.WriteLineAsync(ex.Message);
            PrintUsage();
            throw new Exception($"Invalid command line arguments. Check Console for more information.");
        }

        if (!TryReadRequiredOption(options, "name", out string organizationName)
            || !TryReadRequiredOption(options, "description", out string organizationDescription)
            || !TryReadRequiredOption(options, "max-members", out string rawMaxMembers)
            || !TryReadRequiredOption(options, "organizer-username", out string organizerUserName)
            || !TryReadRequiredOption(options, "organizer-email",
                out string organizerEmail)) // If TryReadRequiredOption() fails (checks for all options)
        {
            PrintUsage();
            throw new Exception($"Invalid command line arguments. Make sure you filled out all required parameters.");
        }

        if (!int.TryParse(rawMaxMembers, out int maxMembers))
        {
            await Console.Error.WriteLineAsync("--max-members must be an integer.");
            throw new Exception("Argument for max-members must be a whole positive number.");
        }

        IOrganizationAdminService adminService = provider.GetRequiredService<IOrganizationAdminService>();

// Try creating new Organisation with all needed parameters
        try
        {
            CreateOrganizationResult result = await adminService.CreateOrganizationAsync(new CreateOrganizationRequest
            {
                OrganizationName = organizationName,
                OrganizationDescription = organizationDescription,
                maxUsers = maxMembers,
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
            await Console.Error.WriteLineAsync($"Failed to create organization: {ex.Message}");
            throw new Exception($"Failed to create organization. Check the console for more information.");
        }
    }

    // Function for parsing the options from the input command
    private static Dictionary<string, string> ParseOptions(IEnumerable<string> optionArgs)
    {
        var options = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        string? pendingKey = null; // Remembers last key
        string? pendingValue = null; // Accumulates the value for the current option

        // Loop over every given argument in the command
        foreach (string token in optionArgs)
        {
            if (token.StartsWith("--", StringComparison.Ordinal)) // If token starts with '--' (option)
            {
                if (pendingKey is not null) // If there's a pending key, finalize its value
                {
                    if (pendingValue == null)
                    {
                        throw new ArgumentException($"Missing value for --{pendingKey}");
                    }
                    options[pendingKey] = pendingValue;
                    pendingValue = null;
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

            // Accumulate the value (supports multi-word values)
            if (pendingValue == null)
            {
                pendingValue = token;
            }
            else
            {
                pendingValue += " " + token;
            }
        }

        if (pendingKey is not null)
        {
            if (pendingValue == null)
            {
                throw new ArgumentException($"Missing value for --{pendingKey}");
            }
            options[pendingKey] = pendingValue;
        }

        return options;
    }

// Function checks if the key is not in the dicionary or if the value is empty and the throws an error
    private static bool TryReadRequiredOption(
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
    private static void PrintUsage()
    {
        Console.WriteLine("Usage:");
        Console.WriteLine(
            "  create-org --name <organizationName> --description <description> --max-members <number> --organizer-username <userName> --organizer-email <email>");
    }
}