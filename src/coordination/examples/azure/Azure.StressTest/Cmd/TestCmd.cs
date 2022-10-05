using Petabridge.Cmd;

namespace Azure.StressTest.Cmd
{
    internal static class TestCmd
    {
        public static readonly CommandDefinition ShutDownActorSystem = new CommandDefinitionBuilder()
            .WithName("shutdown").WithDescription("Shut down the running ActorSystem in the remote node")
            .WithArgument(a => a.WithName("delay")
                .IsMandatory(false).WithDefaultValue("0").WithSwitch("-d").WithSwitch("-D")
                .WithDescription("Add a delay after the shutdown, in seconds"))
            .Build();
        
        public static readonly CommandDefinition KillActorSystem = new CommandDefinitionBuilder()
            .WithName("crash").WithDescription("Crash the running ActorSystem in the remote node")
            .WithArgument(a => a.WithName("delay")
                .IsMandatory(false).WithDefaultValue("0").WithSwitch("-d").WithSwitch("-D")
                .WithDescription("Add a delay after the crash, in seconds"))
            .Build();

        public static readonly CommandDefinition KillProcess = new CommandDefinitionBuilder()
            .WithName("kill").WithDescription("Kill the process the ActorSystem is running in")
            .WithArgument(a => a.WithName("code")
                .IsMandatory(false).WithDefaultValue("0").WithSwitch("-c").WithSwitch("-C")
                .WithDescription("Set the exit code"))
            .Build();
        
        public static readonly CommandPalette TestCmdPalette = new CommandPalette("test", 
            new[] { ShutDownActorSystem, KillActorSystem, KillProcess });
    }

    internal sealed class TestCommandHandler : CommandHandlerActor
    {
        public TestCommandHandler(ExtendedActorSystem system) : base(TestCmd.TestCmdPalette)
        {
            Process(TestCmd.ShutDownActorSystem.Name, (command, args) =>
            {
                int.TryParse(args.ArgumentValues("code").FirstOrDefault(), out var delay);
                system.Terminate().Wait();
                if(delay > 0)
                    Thread.Sleep(TimeSpan.FromSeconds(delay));
            });
            
            Process(TestCmd.KillActorSystem.Name, (command, args) =>
            {
                int.TryParse(args.ArgumentValues("code").FirstOrDefault(), out var delay);     
                system.Guardian.Stop();
                if(delay > 0)
                    Thread.Sleep(TimeSpan.FromSeconds(delay));
            });
            
            Process(TestCmd.KillProcess.Name, (command, args) =>
            {
                int.TryParse(args.ArgumentValues("code").FirstOrDefault(), out var exitCode);
                Environment.Exit(exitCode);
            });
        }
    }

    public sealed class TestCommands : CommandPaletteHandler
    {
        private ExtendedActorSystem? _system;
        private Props? _handlerProps;
        
        public TestCommands() : base(TestCmd.TestCmdPalette)
        {
        }

        public override Props HandlerProps
        {
            get
            {
                if (_handlerProps != null)
                    return _handlerProps;

                if (_system == null)
                    throw new InvalidOperationException("Must call OnRegister first");

                _handlerProps = Props.Create(() => new TestCommandHandler(_system));
                return _handlerProps;
            }
        }

        public override void OnRegister(PetabridgeCmd plugin)
        {
            _system = plugin.Sys;
        }
    }
}