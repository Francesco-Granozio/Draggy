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
        private ShelfWindow? _shelfWindow;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // Inizializza le finestre
            _overlayWindow = new OverlayWindow();
            _shelfWindow = new ShelfWindow();

            // Configura il mouse hook per il futuro (disabilitato per ora)
            // _mouseHook = new MouseHook();
            // _mouseHook.PotentialDragStart += OnPotentialDragStart;

            // Mostra la shelf window
            _shelfWindow.Show();
            
            // Mostra anche l'overlay in posizione fissa per testing
            SetupOverlayWindow();
            
            // La main window può rimanere nascosta (è solo per prototipo)
            MainWindow = _shelfWindow;
        }

        private void SetupOverlayWindow()
        {
            if (_overlayWindow != null)
            {
                // Posiziona l'overlay in un angolo dello schermo per testing
                _overlayWindow.Width = 200;
                _overlayWindow.Height = 150;
                _overlayWindow.Left = SystemParameters.PrimaryScreenWidth - 220;
                _overlayWindow.Top = 20;
                _overlayWindow.Show();
            }
        }

        private void OnPotentialDragStart(System.Windows.Point point)
        {
            // Logica disabilitata per ora - utilizziamo overlay fisso
        }

        protected override void OnExit(ExitEventArgs e)
        {
            // Cleanup
            _mouseHook?.Dispose();
            base.OnExit(e);
        }
    }
}
