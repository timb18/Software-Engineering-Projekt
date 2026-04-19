using Cli;
using Microsoft.Extensions.DependencyInjection;
using Services;

CommandParser parser = new CommandParser();

await parser.ParseCommand(args);
