using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace MSX_SpritesEditor
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        void App_Startup(object sender, StartupEventArgs e)
        {
            MainWindowViewModel mainWindowVM = new MainWindowViewModel();

            MainWindow mw = new MainWindow(mainWindowVM);

            mw.Show();
        }
    }
}
