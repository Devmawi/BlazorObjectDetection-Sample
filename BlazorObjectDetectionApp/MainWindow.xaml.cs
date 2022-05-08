using Microsoft.AspNetCore.Components.WebView.Wpf;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace BlazorObjectDetectionApp
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            InitializeBlazorWebView();
        }

        public void InitializeBlazorWebView()
        {

            var serviceCollection = new ServiceCollection();
            serviceCollection.AddWpfBlazorWebView();
#if DEBUG
            serviceCollection.AddBlazorWebViewDeveloperTools(); // Comes later: https://github.com/dotnet/maui/commit/cfc3fab4b07db3c5aeabf20819efc7b140144215
#endif

            mainBlazorWebView.HostPage = "wwwroot\\index.html";
            mainBlazorWebView.Services = serviceCollection.BuildServiceProvider();

            var rootComponent = new RootComponent();
            rootComponent.Selector = "#app";
            rootComponent.ComponentType = typeof(MainComponent);
            mainBlazorWebView.RootComponents.Add(rootComponent);

            // See also https://github.com/dotnet/maui/issues/3861
            //var userData = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "BlazorWinFormsApp");
            ////Directory.CreateDirectory(userData);
            //var creationProperties = new CoreWebView2CreationProperties()
            //{
            //    UserDataFolder = userData
            //};
            //mainBlazorWebView.WebView.CreationProperties = creationProperties;
            //mainBlazorWebView.WebView.CoreWebView2InitializationCompleted += WebView_CoreWebView2InitializationCompleted;
        }
    }
}
