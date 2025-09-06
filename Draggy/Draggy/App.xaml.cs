using Draggy.Services;
using System.Configuration;
using System.Data;
using System.Windows;

namespace Draggy
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        private MouseHook? _mouseHook;
        private OverlayWindow? _overlayWindow;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // Pulisci la cache e i thumbnail all'avvio per evitare duplicazioni come "file (1).png"
            Services.CacheService.ClearCache();
            Services.ThumbnailCache.ClearCache();

            // Inizializza solo l'overlay window unificato
            _overlayWindow = new OverlayWindow();
            
            // Configura il mouse hook per rilevare i drag globalmente
            _mouseHook = new MouseHook();
            _mouseHook.PotentialDragStart += OnPotentialDragStart;

            // Posiziona l'overlay ma non mostrarlo inizialmente
            SetupOverlayWindow();
            
            // Sottoscrivi ai cambiamenti degli items per gestire la visibilità
            ViewModels.ShelfViewModel.Instance.ItemsChanged += OnItemsChanged;
            
            // La main window è ora l'overlay
            MainWindow = _overlayWindow;

            // Mantieni sempre creata e visibile, ma nascosta fuori dallo schermo quando vuota
            _overlayWindow.Show();
            MoveOverlayOffscreen();

            // Avvia pulizia periodica della cache (solo file orfani per non rompere gli item correnti)
            StartPeriodicCacheCleanup();
        }

        private void SetupOverlayWindow()
        {
            if (_overlayWindow != null)
            {
                // Calcola la posizione iniziale una sola volta
                double initialLeft = SystemParameters.PrimaryScreenWidth - 370;
                double initialTop = 20;
                
                // Imposta e salva la posizione iniziale
                _overlayWindow.Left = initialLeft;
                _overlayWindow.Top = initialTop;
                _savedLeft = initialLeft;
                _savedTop = initialTop;
                
                System.Diagnostics.Debug.WriteLine($"Posizione iniziale impostata: ({initialLeft}, {initialTop})");
                
                // Non mostrare la finestra inizialmente
            }
        }

        private bool _wasShownByDrag = false;
        private double _savedLeft = 0;
        private double _savedTop = 0;

        public void ResetDragFlag()
        {
            _wasShownByDrag = false;
        }

        public void UpdateSavedPosition(double left, double top)
        {
            _savedLeft = left;
            _savedTop = top;
        }

        private void OnItemsChanged(bool hasItems)
        {
            if (_overlayWindow != null)
            {
                System.Diagnostics.Debug.WriteLine($"OnItemsChanged: hasItems={hasItems}, IsVisible={_overlayWindow.IsVisible}, _wasShownByDrag={_wasShownByDrag}");
                
                if (hasItems)
                {
                    // Riporta la finestra nella posizione salvata, evitando Show/Hide durante il drag
                    _overlayWindow.Left = _savedLeft;
                    _overlayWindow.Top = _savedTop;
                    System.Diagnostics.Debug.WriteLine($"Finestra portata in posizione ({_savedLeft}, {_savedTop})");
                }
                else
                {
                    // Mantieni la finestra viva ma portala fuori dallo schermo
                    _savedLeft = _overlayWindow.Left;
                    _savedTop = _overlayWindow.Top;
                    MoveOverlayOffscreen();
                    System.Diagnostics.Debug.WriteLine($"Finestra spostata off-screen, posizione salvata ({_savedLeft}, {_savedTop})");
                }
            }
        }

        private void OnPotentialDragStart(System.Windows.Point point)
        {
            // Quando viene rilevato un potenziale drag, mostra l'overlay ma disabilita temporaneamente il drop
            if (_overlayWindow != null)
            {
                // Riporta la finestra nella posizione salvata (senza Show/Hide) per non perdere il drag
                _overlayWindow.Left = _savedLeft;
                _overlayWindow.Top = _savedTop;
                _wasShownByDrag = true;
                
                // Disabilita temporaneamente il drop per evitare interferenze
                _overlayWindow.AllowDrop = false;
                
                // Riabilita il drop dopo un breve delay
                var timer = new System.Windows.Threading.DispatcherTimer
                {
                    Interval = TimeSpan.FromMilliseconds(300)
                };
                timer.Tick += (s, args) =>
                {
                    timer.Stop();
                    if (_overlayWindow != null)
                    {
                        _overlayWindow.AllowDrop = true;
                        System.Diagnostics.Debug.WriteLine("Drop riabilitato dopo delay");
                    }
                };
                timer.Start();
                
                System.Diagnostics.Debug.WriteLine($"Drag rilevato a ({point.X}, {point.Y}), overlay posizionato in ({_savedLeft}, {_savedTop}) con drop temporaneamente disabilitato");
            }
        }

        protected override void OnExit(ExitEventArgs e)
        {
            // Cleanup
            _mouseHook?.Dispose();
            base.OnExit(e);
        }

        private System.Windows.Threading.DispatcherTimer? _cacheCleanupTimer;
        private void StartPeriodicCacheCleanup()
        {
            _cacheCleanupTimer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromMinutes(10)
            };
            _cacheCleanupTimer.Tick += (s, args) =>
            {
                try
                {
                    var items = ViewModels.ShelfViewModel.Instance.Items;
                    if (items.Count == 0)
                    {
                        // Nessun item in shelf: pulisci completamente
                        Services.CacheService.ClearCache();
                        // I thumbnail verranno ricreati al bisogno
                        Services.ThumbnailCache.ClearCache();
                    }
                    else
                    {
                        // Rimuovi solo i file orfani non più referenziati dagli item correnti
                        var activePaths = new System.Collections.Generic.HashSet<string>(items.Select(i => i.FilePath), System.StringComparer.OrdinalIgnoreCase);
                        Services.CacheService.ClearOrphanedFiles(activePaths);
                    }
                }
                catch { }
            };
            _cacheCleanupTimer.Start();
        }

        private void MoveOverlayOffscreen()
        {
            if (_overlayWindow == null)
                return;

            double offLeft = SystemParameters.VirtualScreenLeft + SystemParameters.VirtualScreenWidth + 2000;
            double offTop = SystemParameters.VirtualScreenTop + SystemParameters.VirtualScreenHeight + 2000;
            _overlayWindow.Left = offLeft;
            _overlayWindow.Top = offTop;
        }
    }
}
