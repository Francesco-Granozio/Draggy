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

            // Inizializza solo l'overlay window unificato
            _overlayWindow = new OverlayWindow();
            
            // Configura il mouse hook per il futuro (disabilitato per ora)
            // _mouseHook = new MouseHook();
            // _mouseHook.PotentialDragStart += OnPotentialDragStart;

            // Posiziona e mostra l'overlay unificato
            SetupOverlayWindow();
            
            // La main window è ora l'overlay
            MainWindow = _overlayWindow;
        }

        private void SetupOverlayWindow()
        {
            if (_overlayWindow != null)
            {
                // Posiziona l'overlay in un angolo dello schermo
                _overlayWindow.Left = SystemParameters.PrimaryScreenWidth - 370;
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
