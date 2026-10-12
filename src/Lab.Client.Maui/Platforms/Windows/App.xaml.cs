using Microsoft.UI.Xaml;

namespace Lab.Client.Maui.WinUI;

/// <summary>
/// Точка входу клієнта MAUI для Windows.
/// </summary>
public partial class App : MauiWinUIApplication
{
	/// <summary>
	/// Створює Windows-застосунок і завантажує його ресурси.
	/// </summary>
	public App()
	{
		this.InitializeComponent();
	}

	protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();
}
