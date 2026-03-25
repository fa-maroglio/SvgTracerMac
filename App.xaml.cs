using Microsoft.Extensions.DependencyInjection;

namespace SvgTracerMac {
    public partial class App : Application {

        #region METODI PUBBLICI

        public App() {
            InitializeComponent();

        }

        protected override Window CreateWindow(IActivationState? activationState) {
            var window = new Window(new AppShell());

            /*  Dimensione finestra 16:10  */
            window.Width = 1024;
            window.Height = 640;
            window.MinimumWidth = 800;
            window.MinimumHeight = 500;

            return window;

        }

        #endregion

    }

}