using CommunityToolkit.Maui;
using Microsoft.Extensions.Logging;

namespace SvgTracerMac {
    public static class MauiProgram {

        #region METODI PUBBLICI

        public static MauiApp CreateMauiApp() {
            var builder = MauiApp.CreateBuilder();
            builder
                .UseMauiApp<App>()
                .UseMauiCommunityToolkit()
                .ConfigureFonts(fonts => {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                    fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");

                });

#if DEBUG
            builder.Logging.AddDebug();
#endif

            return builder.Build();

        }

        #endregion

    }

}
