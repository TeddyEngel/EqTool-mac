using Avalonia.Threading;
using EQTool.Avalonia.Services;
using EQTool.Models;
using EQTool.Services;
using EQTool.Services.Map;
using EQTool.ViewModels;
using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Media.Media3D;

namespace EQTool.Avalonia.ViewModels
{
    public class MapWindowViewModel : INotifyPropertyChanged, IDisposable
    {
        private readonly MapLoad mapLoad;
        private readonly ActivePlayer activePlayer;
        private readonly LogEvents logEvents;

        private ParsedData mapData;
        private Point3D? playerLocation;
        private string playerName;
        private string zoneName;
        private string loadedZone;
        private string noticeLine;

        public MapWindowViewModel() : this(AppServices.Initialize())
        {
        }

        public MapWindowViewModel(AppServices services)
        {
            mapLoad = services.Resolve<MapLoad>();
            activePlayer = services.Resolve<ActivePlayer>();
            logEvents = services.Resolve<LogEvents>();

            logEvents.YouZonedEvent += OnYouZoned;
            logEvents.PlayerLocationEvent += OnPlayerLocation;

            LoadZone(activePlayer.Player?.Zone);
        }

        public ParsedData MapData
        {
            get => mapData;
            private set { mapData = value; OnPropertyChanged(); }
        }

        public Point3D? PlayerLocation
        {
            get => playerLocation;
            private set { playerLocation = value; OnPropertyChanged(); }
        }

        public string PlayerName
        {
            get => playerName;
            private set { playerName = value; OnPropertyChanged(); }
        }

        public string ZoneName
        {
            get => zoneName;
            private set { zoneName = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasZone)); }
        }

        public bool HasZone => !string.IsNullOrWhiteSpace(zoneName);

        public string NoticeLine
        {
            get => noticeLine;
            private set { noticeLine = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasNotice)); }
        }

        public bool HasNotice => !string.IsNullOrEmpty(noticeLine);

        // Zone files are keyed on the short name. A zone with no map file is normal
        // rather than an error, so it reports rather than throwing.
        private void LoadZone(string zone)
        {
            if (string.IsNullOrWhiteSpace(zone))
            {
                NoticeLine = "Waiting for a zone. Enter a zone in game, or type /loc.";
                return;
            }

            if (string.Equals(zone, loadedZone, StringComparison.OrdinalIgnoreCase))
                return;

            var parsed = mapLoad.Load(zone);
            loadedZone = zone;
            ZoneName = zone;
            MapData = parsed;

            NoticeLine = parsed == null || parsed.Lines.Count == 0
                ? "No map data for " + zone + "."
                : null;
        }

        private void OnYouZoned(object sender, YouZonedEvent e)
        {
            Dispatcher.UIThread.Post(() => LoadZone(e?.ShortName));
        }

        private void OnPlayerLocation(object sender, PlayerLocationEvent e)
        {
            if (e == null)
                return;

            Dispatcher.UIThread.Post(() =>
            {
                PlayerLocation = e.Location;
                PlayerName = e.PlayerInfo?.Name ?? activePlayer.Player?.Name;

                // A /loc can arrive before the zone line is seen, so treat it as a
                // second chance to work out which map to show.
                LoadZone(e.PlayerInfo?.Zone ?? activePlayer.Player?.Zone);
            });
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public void Dispose()
        {
            logEvents.YouZonedEvent -= OnYouZoned;
            logEvents.PlayerLocationEvent -= OnPlayerLocation;
        }
    }
}
