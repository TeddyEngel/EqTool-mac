using System;
using System.Linq;
using Autofac;
using Autofac.Features.ResolveAnything;
using EQTool.Models;
using EQTool.Services;
using EQTool.Services.Handlers;

namespace EQTool.Avalonia.Services
{
    // Autofac composition for the native client.
    //
    // Modelled on `EQtoolsTests/DI.cs`, which is the only non-WPF composition
    // upstream has and is already proven against the linked core. Two
    // deliberate differences:
    //
    //   - `EQToolSettings` and `EQToolSettingsLoad` are supplied as already-built
    //     instances, because settings must be loaded and path-normalised before
    //     `LogParser` exists (its 100 ms poll starts inside its constructor).
    //   - `IAppDispatcher` resolves to `AvaloniaAppDispatcher`, not the
    //     synchronous stub in `EQTool.Core/Compat/EqToolStubs.cs`.
    public static class NativeContainer
    {
        public static IContainer Build(SettingsBootstrapResult bootstrap)
        {
            if (bootstrap == null)
                throw new ArgumentNullException(nameof(bootstrap));

            var builder = new ContainerBuilder();
            _ = builder.RegisterSource(new AnyConcreteTypeNotAlreadyRegisteredSource());

            _ = builder.RegisterInstance(bootstrap.Settings).AsSelf().SingleInstance();
            _ = builder.RegisterInstance(bootstrap.Loader).AsSelf().SingleInstance();

            _ = builder.RegisterType<AvaloniaAppDispatcher>().As<IAppDispatcher>().SingleInstance();
            _ = builder.RegisterType<SilentTextToSpeach>().As<ITextToSpeach>().SingleInstance();

            RegisterCoreTypes(builder);

            _ = builder.RegisterType<LogEvents>().AsSelf().SingleInstance();
            _ = builder.RegisterType<SpellIcons>().AsSelf().SingleInstance();
            _ = builder.RegisterType<ParseSpells_spells_us>().AsSelf().SingleInstance();
            _ = builder.RegisterType<EQTool.ViewModels.SettingsWindowViewModel>().AsSelf().SingleInstance();
            _ = builder.RegisterType<EQSpells>().AsSelf().SingleInstance();
            _ = builder.RegisterType<EQTool.ViewModels.ActivePlayer>().AsSelf().SingleInstance();
            _ = builder.RegisterType<EQTool.ViewModels.SpellWindowViewModel>().AsSelf().SingleInstance();
            _ = builder.RegisterType<EQTool.ViewModels.MobInfoComponents.PetViewModel>().AsSelf().SingleInstance();
            _ = builder.RegisterType<EQTool.ViewModels.MobInfoComponents.MobInfoViewModel>().AsSelf().SingleInstance();
            _ = builder.RegisterType<EQTool.ViewModels.MobInfoComponents.MobInfoManagementViewModel>().AsSelf().SingleInstance();
            _ = builder.RegisterType<EQTool.ViewModels.DPSWindowViewModel>().AsSelf().SingleInstance();
            _ = builder.RegisterType<EQToolShared.Discord.DiscordAuctionParse>().AsSelf().SingleInstance();
            _ = builder.RegisterType<AudioService>().As<IAudioService>().SingleInstance();
            _ = builder.RegisterType<FightHistory>().AsSelf().SingleInstance();
            _ = builder.RegisterType<SpellDurations>().AsSelf().SingleInstance();

            // FileReader carries the tail offset for the log file. LogParser takes
            // the concrete type, handlers take the interface; both must be the
            // same instance or the file would be read from two positions.
            _ = builder.RegisterType<EQTool.Services.IO.FileReader>()
                .AsSelf()
                .As<EQTool.Services.IO.IFileReader>()
                .SingleInstance();

            _ = builder.RegisterType<Pets>().AsSelf().SingleInstance();
            _ = builder.RegisterType<TriggerTimerManager>().AsSelf().SingleInstance();
            _ = builder.RegisterType<LogParser>().AsSelf().SingleInstance();

            return builder.Build();
        }

        // Parsers and handlers are discovered rather than listed, exactly as
        // DI.cs does it, so a new upstream parser is picked up by a rebuild.
        // The assembly is taken from a known type instead of scanning
        // AppDomain, because EQTool.Core may not be loaded yet at this point.
        private static void RegisterCoreTypes(ContainerBuilder builder)
        {
            var coreTypes = typeof(LogParser).Assembly.GetTypes();

            foreach (var type in coreTypes.Where(a => a.IsClass && !a.IsAbstract))
            {
                if (type.GetInterfaces().Contains(typeof(IEqLogParser)))
                    _ = builder.RegisterType(type).As<IEqLogParser>().SingleInstance();
            }

            foreach (var type in coreTypes.Where(a => !a.IsAbstract))
            {
                if (type.IsSubclassOf(typeof(BaseHandler)))
                    _ = builder.RegisterType(type).As<BaseHandler>().SingleInstance();
            }
        }
    }
}
