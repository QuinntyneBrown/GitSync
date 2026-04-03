using System.CommandLine;
using GitSync.Commands;

var command = new SyncCommand();
return await command.InvokeAsync(args);

