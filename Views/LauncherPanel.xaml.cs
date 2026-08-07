using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace DynamicIsland;

public partial class LauncherPanel : UserControl
{
	private class VersionComparer : IComparer<string>
	{
		public int Compare(string? x, string? y)
		{
			if (x == null || y == null)
			{
				return 0;
			}
			string[] array = x.Split('.');
			string[] array2 = y.Split('.');
			for (int i = 0; i < Math.Min(array.Length, array2.Length); i++)
			{
				if (int.TryParse(array[i], out var result) && int.TryParse(array2[i], out var result2))
				{
					if (result != result2)
					{
						return result.CompareTo(result2);
					}
					continue;
				}
				return string.CompareOrdinal(array[i], array2[i]);
			}
			return array.Length.CompareTo(array2.Length);
		}
	}

	private TranslateTransform _panelSlide;

	private readonly List<(string id, string type)> _allVersions = new List<(string, string)>();

	private static readonly Brush CardBgBrush;

	private static readonly Brush CardHoverBrush;

	private static readonly Brush CardSelectedBrush;

	private static readonly Brush IconBgBrush;

	private static readonly Brush TextWhiteBrush;

	private static readonly Brush TextDescBrush;

	private static readonly Brush TextGrayBrush;

	private static readonly Brush TextHintBrush;

	private static readonly Brush TagBgBrush;

	private static readonly Brush VersionCardBgBrush;

	private static readonly Brush VersionCardHoverBrush;

	private static readonly Brush VersionCardBorderBrush;

	private static readonly Brush CategoryInactiveBgBrush;

	private static readonly Brush CategoryInactiveTextBrush;

	private static readonly Brush ErrorBrush;

	private static readonly Brush HintBrush;

	private static readonly HttpClient SharedImageClient;

	private static readonly DropShadowEffect SharedCardShadow;

	private string _selectedVersionId = "";

	private string? _selectedLoaderName = null;

	private string? _selectedLoaderVersion = null;

	private string _currentCategory = "全部";

	private Border? _selectedCategoryBorder = null;

	private int _versionRenderToken;

	private int _listEnterMode;

	private int _staggerIndex;

	private static readonly IEasingFunction EaseOut;

	private static readonly IEasingFunction EaseIn;

	private string _currentResourceType = "游戏";

	private List<ModrinthProject> _searchResults = new List<ModrinthProject>();

	private Border? _taskCard;

	private ProgressBar? _taskCardProgress;

	private TextBlock? _taskCardSpeed;

	private TextBlock? _taskCardStep;

	private TextBlock? _taskCardStatus;

	private TextBlock? _taskCardTitle;

	private DateTime _lastCardUpdate = DateTime.MinValue;

	private DownloadStep _lastTaskStep = DownloadStep.Idle;

	private string _lastTaskName = "";

	private readonly Dictionary<ScrollViewer, double> _scrollTargets = new Dictionary<ScrollViewer, double>();

	private readonly Dictionary<ScrollViewer, double> _scrollStartOffsets = new Dictionary<ScrollViewer, double>();

	private readonly Dictionary<ScrollViewer, DateTime> _scrollStartTimes = new Dictionary<ScrollViewer, DateTime>();

	private const double ScrollDurationMs = 350.0;

	private const double ScrollStep = 90.0;

	private bool _scrolling = false;

	private Border? _versionSelectOverlay;

	private Border? _versionSelectPanel;

	private string? _selectedLaunchVersionId;

	private Border? _selectedItem = null;

	private Color _currentThemeColor = Color.FromRgb(72, 144, 245);

	private string? _verSettingsVersionId;

	private string? _returnPageAfterVerSettings;

	private bool _languageApplying = false;

	private bool _settingsInitialized = false;

	private bool _suppressConfigSave = false;

	private Border? _selectedSettingsTab = null;

	private static readonly string[] SettingsTabKeys;

	private static readonly string[] ThemeNames;

	public event EventHandler? CollapseRequested;

	public event EventHandler? ExitRequested;

	public LauncherPanel()
	{
		InitializeComponent();
		_panelSlide = PanelSlide;
		base.Loaded += LauncherPanel_Loaded;
		DownloadManager.ProgressChanged += OnDownloadProgressChanged;
		DownloadManager.DownloadCompleted += OnDownloadCompleted;
		DownloadManager.DownloadFailed += OnDownloadFailed;
		LaunchManager.LaunchFailed += OnLaunchFailed;
		LaunchManager.LaunchCompleted += OnLaunchCompleted;
		LaunchManager.ProgressChanged += OnLaunchProgressChanged;
		LauncherConfig.Changed += OnConfigChanged;
		UpdateChecker.UpdateAvailable += OnUpdateAvailable;
	}

	private void OnUpdateAvailable(UpdateInfo info)
	{
		UpdateInfo info2 = info;
		base.Dispatcher.BeginInvoke((Action)delegate
		{
			try
			{
				string arg = info2.TagName.TrimStart('v', 'V');
				NotificationManager.Show(string.Format(LanguageManager.Get("MsgUpdateFound"), arg));
				if (!string.IsNullOrEmpty(info2.HtmlUrl))
				{
					Process.Start(new ProcessStartInfo(info2.HtmlUrl)
					{
						UseShellExecute = true
					});
				}
			}
			catch
			{
			}
		});
	}

	private void OnLaunchProgressChanged(LaunchProgress p)
	{
		LaunchProgress p2 = p;
		base.Dispatcher.BeginInvoke((Action)delegate
		{
			if (!LaunchButton.IsEnabled)
			{
				LaunchButton.Content = $"{p2.Stage} {p2.Progress:0}%";
			}
		});
	}

	private void OnLaunchCompleted()
	{
		base.Dispatcher.BeginInvoke((Action)delegate
		{
			LaunchButton.IsEnabled = true;
			LaunchButton.SetResourceReference(ContentControl.ContentProperty, "LaunchStart");
			LaunchStatusText.SetResourceReference(TextBlock.TextProperty, "LaunchSuccess");
			LaunchStatusText.Foreground = new SolidColorBrush(Color.FromRgb(80, 200, 120));
			LaunchStatusText.Visibility = Visibility.Visible;
		});
	}

	private void OnLaunchFailed(Exception ex)
	{
		base.Dispatcher.BeginInvoke((Action)delegate
		{
			LaunchButton.IsEnabled = true;
			LaunchButton.SetResourceReference(ContentControl.ContentProperty, "LaunchStart");
			LaunchStatusText.SetResourceReference(TextBlock.TextProperty, "LaunchFailed");
			LaunchStatusText.Foreground = new SolidColorBrush(Color.FromRgb(232, 90, 90));
			LaunchStatusText.Visibility = Visibility.Visible;
			LaunchStatusText.ToolTip = null;
		});
	}

	private void LauncherPanel_Loaded(object sender, RoutedEventArgs e)
	{
		_panelSlide.X = -200.0;
		base.Opacity = 0.0;
		base.Visibility = Visibility.Collapsed;
		LanguageManager.Apply(LauncherConfig.Current.Language);
		InitSettingsTabs();
		InitSettingsControls();
		ApplyConfigToLaunchPage();
		ApplyPersonalization();
	}

	public static double GetAnimationSpeedFactor()
	{
		return (double)LauncherConfig.Current.AnimationSpeed / 100.0;
	}

	public static TimeSpan AnimDuration(double baseMs)
	{
		double num = GetAnimationSpeedFactor();
		if (num < 0.05)
		{
			num = 0.05;
		}
		return TimeSpan.FromMilliseconds(baseMs / num);
	}

	private static SolidColorBrush Freeze(Color c)
	{
		SolidColorBrush solidColorBrush = new SolidColorBrush(c);
		solidColorBrush.Freeze();
		return solidColorBrush;
	}

	private static HttpClient CreateImageClient()
	{
		HttpClient httpClient = new HttpClient
		{
			Timeout = TimeSpan.FromSeconds(10L)
		};
		httpClient.DefaultRequestHeaders.Add("User-Agent", "DIL/1.0");
		return httpClient;
	}

	static LauncherPanel()
	{
		CardBgBrush = Freeze(Color.FromRgb(42, 42, 46));
		CardHoverBrush = Freeze(Color.FromRgb(52, 52, 56));
		CardSelectedBrush = Freeze(Color.FromRgb(72, 144, 245));
		IconBgBrush = Freeze(Color.FromRgb(55, 55, 58));
		TextWhiteBrush = Freeze(Colors.White);
		TextDescBrush = Freeze(Color.FromRgb(140, 140, 140));
		TextGrayBrush = Freeze(Color.FromRgb(170, 170, 170));
		TextHintBrush = Freeze(Color.FromRgb(120, 120, 120));
		TagBgBrush = Freeze(Color.FromRgb(90, 90, 94));
		VersionCardBgBrush = Freeze(Color.FromRgb(45, 45, 48));
		VersionCardHoverBrush = Freeze(Color.FromRgb(62, 62, 66));
		VersionCardBorderBrush = Freeze(Color.FromRgb(60, 60, 64));
		CategoryInactiveBgBrush = Freeze(Color.FromRgb(40, 40, 44));
		CategoryInactiveTextBrush = Freeze(Color.FromRgb(170, 170, 170));
		ErrorBrush = Freeze(Color.FromRgb(245, 80, 80));
		HintBrush = Freeze(Color.FromRgb(136, 136, 136));
		SharedImageClient = CreateImageClient();
		SharedCardShadow = new DropShadowEffect
		{
			Color = Colors.Black,
			Opacity = 0.15,
			BlurRadius = 8.0,
			ShadowDepth = 1.0
		};
		EaseOut = FreezeEase(new CubicEase
		{
			EasingMode = EasingMode.EaseOut
		});
		EaseIn = FreezeEase(new CubicEase
		{
			EasingMode = EasingMode.EaseIn
		});
		SettingsTabKeys = new string[4] { "launch", "ui", "link", "system" };
		ThemeNames = new string[15]
		{
			"龙猫蓝", "甜柠青", "小草绿", "菠萝黄", "橡木棕", "玄素黑", "滑稽彩", "铁杆粉", "神秘紫", "欧皇彩",
			"秋仪金", "活跃橙", "跳票红", "极客蓝", "自定义"
		};
		SharedCardShadow.Freeze();
	}

	private static IEasingFunction FreezeEase(IEasingFunction e)
	{
		return e;
	}

	private void InitCategoryFilter()
	{
		CategoryFilterPanel.Children.Clear();
		_selectedCategoryBorder = null;
		string[] array = new string[5] { "全部", "正式版", "快照", "远古", "愚人节" };
		string[] array2 = new string[5] { "CatAll", "CatRelease", "CatSnapshot", "CatOld", "CatAprilFool" };
		for (int i = 0; i < array.Length; i++)
		{
			string category = array[i];
			Border border = new Border
			{
				Height = 28.0,
				Padding = new Thickness(14.0, 0.0, 14.0, 0.0),
				Margin = new Thickness(0.0, 0.0, 6.0, 0.0),
				CornerRadius = new CornerRadius(14.0),
				Cursor = Cursors.Hand,
				Tag = category
			};
			TextBlock textBlock = new TextBlock
			{
				FontSize = 12.0,
				VerticalAlignment = VerticalAlignment.Center,
				HorizontalAlignment = HorizontalAlignment.Center
			};
			textBlock.SetResourceReference(TextBlock.TextProperty, array2[i]);
			border.Child = textBlock;
			border.MouseLeftButtonUp += delegate
			{
				SelectCategory(border, category);
			};
			if (category == "全部")
			{
				SelectCategory(border, category);
			}
			else
			{
				border.Background = CategoryInactiveBgBrush;
				textBlock.Foreground = CategoryInactiveTextBrush;
			}
			CategoryFilterPanel.Children.Add(border);
		}
	}

	private void SelectCategory(Border border, string category)
	{
		if (_selectedCategoryBorder != null)
		{
			_selectedCategoryBorder.Background = CategoryInactiveBgBrush;
			if (_selectedCategoryBorder.Child is TextBlock textBlock)
			{
				textBlock.Foreground = CategoryInactiveTextBrush;
			}
		}
		_selectedCategoryBorder = border;
		border.Background = CardSelectedBrush;
		if (border.Child is TextBlock textBlock2)
		{
			textBlock2.Foreground = TextWhiteBrush;
		}
		_currentCategory = category;
		SwitchCategoryAnimation();
	}

	private bool MatchCategory(string versionId, string versionType)
	{
		bool flag = versionType == "snapshot";
		bool flag2 = versionType == "old_alpha" || versionType == "old_beta";
		bool flag3 = IsAprilFoolsVersion(versionId);
		if (_currentCategory == "全部")
		{
			if (flag && !LauncherConfig.Current.ShowDownloadSnapshot)
			{
				return false;
			}
			if (flag2 && !LauncherConfig.Current.ShowDownloadOldBeta)
			{
				return false;
			}
			if (flag3 && !LauncherConfig.Current.ShowDownloadAprilFool)
			{
				return false;
			}
			return true;
		}
		string currentCategory = _currentCategory;
		if (1 == 0)
		{
		}
		bool result = currentCategory switch
		{
			"正式版" => versionType == "release", 
			"快照" => flag, 
			"远古" => flag2, 
			"愚人节" => flag3, 
			_ => true, 
		};
		if (1 == 0)
		{
		}
		return result;
	}

	private static bool IsAprilFoolsVersion(string versionId)
	{
		string versionId2 = versionId;
		string[] source = new string[8] { "2.0", "1.RV-Pre1", "3D Shareware v1.34", "20w14infinite", "22w13oneBlockAtATime", "23w13a_or_b", "24w14potato", "25w14craftmine" };
		return source.Any((string f) => versionId2.Equals(f, StringComparison.OrdinalIgnoreCase));
	}

	private static void ApplyEnterAnimation(FrameworkElement element, int mode, int index)
	{
		if (mode != 0)
		{
			TranslateTransform translateTransform = (TranslateTransform)(element.RenderTransform = new TranslateTransform());
			element.Opacity = 0.0;
			double x = ((mode == 1) ? 45 : (-45));
			int num = ((mode == 2) ? Math.Min(index * 28, 280) : 0);
			int num2 = ((mode == 1) ? 260 : 320);
			translateTransform.X = x;
			DoubleAnimation animation = new DoubleAnimation(0.0, TimeSpan.FromMilliseconds(num2, 0L))
			{
				BeginTime = TimeSpan.FromMilliseconds(num, 0L),
				EasingFunction = EaseOut
			};
			DoubleAnimation animation2 = new DoubleAnimation(1.0, TimeSpan.FromMilliseconds(num2, 0L))
			{
				BeginTime = TimeSpan.FromMilliseconds(num, 0L),
				EasingFunction = EaseOut
			};
			translateTransform.BeginAnimation(TranslateTransform.XProperty, animation);
			element.BeginAnimation(UIElement.OpacityProperty, animation2);
		}
	}

	private bool IsElementInViewport(FrameworkElement element)
	{
		if (VersionScrollViewer == null)
		{
			return true;
		}
		try
		{
			ScrollViewer versionScrollViewer = VersionScrollViewer;
			GeneralTransform generalTransform = element.TransformToVisual(versionScrollViewer);
			Rect rect = generalTransform.TransformBounds(new Rect(0.0, 0.0, element.ActualWidth, element.ActualHeight));
			Rect rect2 = new Rect(0.0, 0.0, versionScrollViewer.ViewportWidth, versionScrollViewer.ViewportHeight);
			return rect.IntersectsWith(rect2);
		}
		catch
		{
			return false;
		}
	}

	private void AnimateExitToLeft(FrameworkElement element)
	{
		if (!IsElementInViewport(element))
		{
			element.Opacity = 0.0;
			return;
		}
		TranslateTransform translateTransform = (TranslateTransform)(element.RenderTransform = new TranslateTransform());
		DoubleAnimation animation = new DoubleAnimation(-55.0, TimeSpan.FromMilliseconds(200L, 0L))
		{
			EasingFunction = EaseIn
		};
		DoubleAnimation animation2 = new DoubleAnimation(0.0, TimeSpan.FromMilliseconds(200L, 0L));
		translateTransform.BeginAnimation(TranslateTransform.XProperty, animation);
		element.BeginAnimation(UIElement.OpacityProperty, animation2);
	}

	private void SwitchCategoryAnimation()
	{
		_versionRenderToken++;
		int token = _versionRenderToken;
		List<FrameworkElement> list = VersionList.Children.OfType<FrameworkElement>().ToList();
		if (list.Count <= 0 || !list.Any((FrameworkElement c) => c.Opacity > 0.05))
		{
			_listEnterMode = 1;
			_staggerIndex = 0;
			RefreshVersionList();
			return;
		}
		foreach (FrameworkElement item in list)
		{
			AnimateExitToLeft(item);
		}
		DispatcherTimer timer = new DispatcherTimer
		{
			Interval = TimeSpan.FromMilliseconds(180L, 0L)
		};
		timer.Tick += delegate
		{
			timer.Stop();
			if (token == _versionRenderToken)
			{
				_listEnterMode = 1;
				_staggerIndex = 0;
				RefreshVersionList();
			}
		};
		timer.Start();
	}

	private void RefreshVersionList()
	{
		_versionRenderToken++;
		int versionRenderToken = _versionRenderToken;
		VersionList.Children.Clear();
		List<(string, string)> list = new List<(string, string)>();
		foreach (var allVersion in _allVersions)
		{
			if (MatchCategory(allVersion.id, allVersion.type))
			{
				list.Add(allVersion);
			}
		}
		if (list.Count == 0)
		{
			TextBlock textBlock = new TextBlock
			{
				Foreground = HintBrush,
				FontSize = 13.0,
				Margin = new Thickness(12.0, 20.0, 12.0, 0.0),
				HorizontalAlignment = HorizontalAlignment.Center
			};
			textBlock.SetResourceReference(TextBlock.TextProperty, "StatusNoVersionInCategory");
			VersionList.Children.Add(textBlock);
		}
		else
		{
			for (int i = 0; i < Math.Min(16, list.Count); i++)
			{
				CreateVersionItem(list[i].Item1, list[i].Item2);
			}
			if (list.Count > 16)
			{
				RenderVersionBatch(list, 16, 16, versionRenderToken);
			}
		}
	}

	private void RenderVersionBatch(List<(string id, string type)> matched, int index, int step, int token)
	{
		List<(string id, string type)> matched2 = matched;
		if (token != _versionRenderToken)
		{
			return;
		}
		int end = Math.Min(index + step, matched2.Count);
		for (int i = index; i < end; i++)
		{
			CreateVersionItem(matched2[i].id, matched2[i].type);
		}
		if (end < matched2.Count)
		{
			base.Dispatcher.BeginInvoke((Action)delegate
			{
				RenderVersionBatch(matched2, end, step, token);
			}, DispatcherPriority.Background);
		}
	}

	private void ShowDownloadPage()
	{
		SwitchToPage(DownloadPage, DownloadPageSlide, delegate
		{
			InitResourceTypeFilter();
			if (_currentResourceType == "游戏")
			{
				GameCategoryBar.Visibility = Visibility.Visible;
				ResourceSearchBar.Visibility = Visibility.Collapsed;
				BottomActionBar.Visibility = Visibility.Visible;
				ListArea.CornerRadius = new CornerRadius(0.0);
				InitCategoryFilter();
				if (_allVersions.Count == 0)
				{
					LoadVersionList();
				}
			}
			else
			{
				GameCategoryBar.Visibility = Visibility.Collapsed;
				ResourceSearchBar.Visibility = Visibility.Visible;
				BottomActionBar.Visibility = Visibility.Collapsed;
				ListArea.CornerRadius = new CornerRadius(0.0, 0.0, 8.0, 8.0);
			}
		});
	}

	private void ShowDownloadCenterPage()
	{
		SwitchToPage(DownloadCenterPage, DownloadCenterPageSlide, delegate
		{
			UpdateDownloadTaskList();
		});
	}

	private void NewDownload_Click(object sender, RoutedEventArgs e)
	{
		SwitchToPage(DownloadPage, DownloadPageSlide, delegate
		{
			_currentResourceType = "游戏";
			InitResourceTypeFilter();
			InitCategoryFilter();
			base.Dispatcher.BeginInvoke((Action)delegate
			{
				LoadVersionList();
			}, DispatcherPriority.Input);
		});
	}

	private void InitResourceTypeFilter()
	{
		ResourceTypePanel.Children.Clear();
		string[] array = new string[4] { "游戏", "模组", "光影", "材质" };
		string[] array2 = new string[4] { "ResTypeGame", "ResTypeMod", "ResTypeShader", "ResTypeMaterial" };
		for (int i = 0; i < array.Length; i++)
		{
			string text = array[i];
			Border border = new Border
			{
				Height = 28.0,
				Padding = new Thickness(14.0, 0.0, 14.0, 0.0),
				Margin = new Thickness(0.0, 0.0, 6.0, 0.0),
				CornerRadius = new CornerRadius(14.0),
				Cursor = Cursors.Hand,
				Tag = text
			};
			TextBlock textBlock = new TextBlock
			{
				FontSize = 12.0,
				VerticalAlignment = VerticalAlignment.Center
			};
			textBlock.SetResourceReference(TextBlock.TextProperty, array2[i]);
			border.Child = textBlock;
			if (text == _currentResourceType)
			{
				border.Background = new SolidColorBrush(Color.FromRgb(72, 144, 245));
				textBlock.Foreground = new SolidColorBrush(Colors.White);
			}
			else
			{
				border.Background = new SolidColorBrush(Color.FromRgb(45, 45, 48));
				textBlock.Foreground = new SolidColorBrush(Color.FromRgb(170, 170, 170));
			}
			border.MouseLeftButtonUp += ResourceType_Click;
			border.MouseEnter += delegate(object s, MouseEventArgs _)
			{
				if (s is Border border3 && border3.Tag as string != _currentResourceType)
				{
					border3.Background = new SolidColorBrush(Color.FromRgb(55, 55, 58));
				}
			};
			border.MouseLeave += delegate(object s, MouseEventArgs _)
			{
				if (s is Border border2 && border2.Tag as string != _currentResourceType)
				{
					border2.Background = new SolidColorBrush(Color.FromRgb(45, 45, 48));
				}
			};
			ResourceTypePanel.Children.Add(border);
		}
	}

	private void ResourceType_Click(object sender, MouseButtonEventArgs e)
	{
		if (!(sender is Border border))
		{
			return;
		}
		string text = (border.Tag as string) ?? "";
		if (text == _currentResourceType)
		{
			return;
		}
		_currentResourceType = text;
		InitResourceTypeFilter();
		_versionRenderToken++;
		VersionList.Children.Clear();
		TextBlock textBlock = new TextBlock
		{
			Foreground = new SolidColorBrush(Color.FromRgb(136, 136, 136)),
			FontSize = 13.0,
			Margin = new Thickness(12.0, 20.0, 12.0, 0.0),
			HorizontalAlignment = HorizontalAlignment.Center
		};
		textBlock.SetResourceReference(TextBlock.TextProperty, "StatusLoading");
		VersionList.Children.Add(textBlock);
		if (text == "游戏")
		{
			GameCategoryBar.Visibility = Visibility.Visible;
			ResourceSearchBar.Visibility = Visibility.Collapsed;
			BottomActionBar.Visibility = Visibility.Visible;
			ListArea.CornerRadius = new CornerRadius(0.0);
			if (_allVersions.Count > 0)
			{
				RefreshVersionList();
				return;
			}
			TextBlock textBlock2 = new TextBlock
			{
				Foreground = new SolidColorBrush(Color.FromRgb(136, 136, 136)),
				FontSize = 13.0,
				Margin = new Thickness(12.0, 20.0, 12.0, 0.0),
				HorizontalAlignment = HorizontalAlignment.Center
			};
			textBlock2.SetResourceReference(TextBlock.TextProperty, "StatusLoadingVersionList");
			VersionList.Children.Add(textBlock2);
			LoadVersionList();
			return;
		}
		GameCategoryBar.Visibility = Visibility.Collapsed;
		ResourceSearchBar.Visibility = Visibility.Visible;
		BottomActionBar.Visibility = Visibility.Collapsed;
		ListArea.CornerRadius = new CornerRadius(0.0, 0.0, 8.0, 8.0);
		ResourceSearchBox.Text = "";
		DoResourceSearch();
		base.Dispatcher.BeginInvoke((Action)delegate
		{
			Focus();
			if (ResourceSearchBox != null)
			{
				ResourceSearchBox.Focus();
				Keyboard.Focus(ResourceSearchBox);
			}
		}, DispatcherPriority.ContextIdle);
	}

	private void ResourceSearch_Click(object sender, RoutedEventArgs e)
	{
		DoResourceSearch();
	}

	private void ResourceSearchBox_PreviewKeyDown(object sender, KeyEventArgs e)
	{
		if (e.Key == Key.Return)
		{
			e.Handled = true;
			base.Dispatcher.BeginInvoke((Action)delegate
			{
				DoResourceSearch();
			}, DispatcherPriority.Background);
		}
	}

	private void ResourceSearchBox_KeyDown(object sender, KeyEventArgs e)
	{
		if (e.Key == Key.Return)
		{
			e.Handled = true;
		}
	}

	private async Task DoResourceSearch()
	{
		string query = ResourceSearchBox.Text.Trim();
		int currentToken = ++_versionRenderToken;
		string currentResourceType = _currentResourceType;
		if (1 == 0)
		{
		}
		ResourceType resourceType = currentResourceType switch
		{
			"模组" => ResourceType.Mod, 
			"光影" => ResourceType.Shader, 
			"材质" => ResourceType.ResourcePack, 
			_ => ResourceType.Mod, 
		};
		if (1 == 0)
		{
		}
		ResourceType resType = resourceType;
		string cacheKey = $"search_{resType}_{query}";
		List<ModrinthProject> cached = DataCache.Get<List<ModrinthProject>>(new object[1] { cacheKey });
		if (cached != null)
		{
			_searchResults = cached;
			RefreshResourceList();
			return;
		}
		VersionList.Children.Clear();
		string displayText = (string.IsNullOrEmpty(query) ? LanguageManager.Get("ResLoadingRecommend") : LanguageManager.Get("ResSearching"));
		TextBlock loading = new TextBlock
		{
			Text = displayText,
			Foreground = new SolidColorBrush(Color.FromRgb(136, 136, 136)),
			FontSize = 13.0,
			Margin = new Thickness(12.0, 20.0, 12.0, 0.0),
			HorizontalAlignment = HorizontalAlignment.Center,
			TextWrapping = TextWrapping.Wrap
		};
		VersionList.Children.Add(loading);
		try
		{
			List<ModrinthProject> result = await Task.Run(() => ModrinthApi.SearchAsync(query, resType));
			if (currentToken == _versionRenderToken)
			{
				DataCache.Set(result, cacheKey);
				_searchResults = result;
				RefreshResourceList();
			}
		}
		catch (Exception ex)
		{
			if (_searchResults.Count == 0)
			{
				VersionList.Children.Clear();
				TextBlock err = new TextBlock
				{
					Text = string.Format(LanguageManager.Get("ResLoadFailed"), ex.Message),
					Foreground = new SolidColorBrush(Color.FromRgb(245, 80, 80)),
					FontSize = 13.0,
					Margin = new Thickness(12.0, 20.0, 12.0, 0.0),
					HorizontalAlignment = HorizontalAlignment.Center
				};
				VersionList.Children.Add(err);
			}
		}
	}

	private void RefreshResourceList()
	{
		if (_currentResourceType == "游戏")
		{
			return;
		}
		_listEnterMode = 2;
		_staggerIndex = 0;
		VersionList.Children.Clear();
		if (_searchResults.Count == 0)
		{
			string text = ResourceSearchBox?.Text?.Trim() ?? "";
			string text2 = (string.IsNullOrEmpty(text) ? LanguageManager.Get("ResNoRecommend") : string.Format(LanguageManager.Get("ResNoResult"), text));
			StackPanel stackPanel = new StackPanel
			{
				HorizontalAlignment = HorizontalAlignment.Center,
				Margin = new Thickness(12.0, 30.0, 12.0, 0.0)
			};
			stackPanel.Children.Add(new TextBlock
			{
				Text = text2,
				Foreground = new SolidColorBrush(Color.FromRgb(136, 136, 136)),
				FontSize = 13.0,
				TextAlignment = TextAlignment.Center,
				HorizontalAlignment = HorizontalAlignment.Center
			});
			VersionList.Children.Add(stackPanel);
			return;
		}
		foreach (ModrinthProject searchResult in _searchResults)
		{
			CreateResourceItem(searchResult);
		}
	}

	private Border CreateTag(string text, Color color)
	{
		Brush background = ((color == Color.FromRgb(72, 144, 245)) ? CardSelectedBrush : ((color == Color.FromRgb(90, 90, 94)) ? TagBgBrush : Freeze(color)));
		return new Border
		{
			Background = background,
			CornerRadius = new CornerRadius(4.0),
			Padding = new Thickness(6.0, 1.0, 6.0, 1.0),
			Margin = new Thickness(0.0, 0.0, 4.0, 0.0),
			Child = new TextBlock
			{
				Text = text,
				Foreground = TextWhiteBrush,
				FontSize = 10.0,
				VerticalAlignment = VerticalAlignment.Center
			}
		};
	}

	private static string FormatVersionRange(List<string> versions)
	{
		if (versions.Count == 0)
		{
			return "";
		}
		var list = (from v in versions
			where v.Contains('.')
			select new
			{
				Raw = v,
				Parts = v.Split('.')
			} into v
			where v.Parts.Length >= 2
			select v).OrderByDescending(v => v.Raw, new VersionComparer()).ToList();
		if (list.Count == 0)
		{
			return versions.First();
		}
		string raw = list.Last().Raw;
		string raw2 = list.First().Raw;
		if (raw == raw2)
		{
			return raw2;
		}
		return raw + " ~ " + raw2;
	}

	private void CreateResourceItem(ModrinthProject proj)
	{
		Border border = new Border
		{
			Background = CardBgBrush,
			CornerRadius = new CornerRadius(8.0),
			Padding = new Thickness(14.0, 10.0, 14.0, 10.0),
			Margin = new Thickness(0.0, 0.0, 0.0, 6.0),
			Cursor = Cursors.Hand,
			Tag = proj
		};
		Grid grid = new Grid();
		grid.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = GridLength.Auto
		});
		grid.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = new GridLength(1.0, GridUnitType.Star)
		});
		grid.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = GridLength.Auto
		});
		Border border2 = new Border
		{
			Width = 44.0,
			Height = 44.0,
			CornerRadius = new CornerRadius(8.0),
			Background = IconBgBrush,
			ClipToBounds = true
		};
		if (!string.IsNullOrEmpty(proj.IconUrl))
		{
			LoadImageAsync(border2, proj.IconUrl);
		}
		else
		{
			border2.Child = new TextBlock
			{
				Text = "?",
				FontSize = 20.0,
				Foreground = TextHintBrush,
				HorizontalAlignment = HorizontalAlignment.Center,
				VerticalAlignment = VerticalAlignment.Center
			};
		}
		Grid.SetColumn(border2, 0);
		StackPanel stackPanel = new StackPanel
		{
			Margin = new Thickness(12.0, 0.0, 0.0, 0.0),
			VerticalAlignment = VerticalAlignment.Center
		};
		TextBlock element = new TextBlock
		{
			Text = proj.Title,
			Foreground = TextWhiteBrush,
			FontSize = 13.0,
			FontWeight = FontWeights.SemiBold,
			TextTrimming = TextTrimming.CharacterEllipsis
		};
		stackPanel.Children.Add(element);
		string text = (string.IsNullOrEmpty(proj.Description) ? LanguageManager.Get("ResNoDesc") : ((proj.Description.Length > 40) ? (proj.Description.Substring(0, 40) + "...") : proj.Description));
		TextBlock element2 = new TextBlock
		{
			Text = text,
			Foreground = TextDescBrush,
			FontSize = 11.0,
			Margin = new Thickness(0.0, 3.0, 0.0, 0.0),
			TextTrimming = TextTrimming.CharacterEllipsis
		};
		stackPanel.Children.Add(element2);
		WrapPanel wrapPanel = new WrapPanel
		{
			Margin = new Thickness(0.0, 4.0, 0.0, 0.0)
		};
		foreach (string item in proj.Loaders.Take(3))
		{
			wrapPanel.Children.Add(CreateTag(item, Color.FromRgb(72, 144, 245)));
		}
		if (proj.GameVersions.Count > 0)
		{
			string text2 = FormatVersionRange(proj.GameVersions);
			if (!string.IsNullOrEmpty(text2))
			{
				wrapPanel.Children.Add(CreateTag(text2, Color.FromRgb(90, 90, 94)));
			}
		}
		stackPanel.Children.Add(wrapPanel);
		Grid.SetColumn(stackPanel, 1);
		Button dlBtn = new Button
		{
			Style = (Style)FindResource("CardButton"),
			Height = 30.0,
			FontSize = 12.0,
			Tag = proj,
			VerticalAlignment = VerticalAlignment.Center
		};
		dlBtn.SetResourceReference(ContentControl.ContentProperty, "DownloadStart");
		dlBtn.Click += ResourceDownload_Click;
		Grid.SetColumn(dlBtn, 2);
		grid.Children.Add(border2);
		grid.Children.Add(stackPanel);
		grid.Children.Add(dlBtn);
		border.Child = grid;
		border.MouseLeftButtonUp += delegate
		{
			ResourceDownload_Click(dlBtn, null);
		};
		border.MouseEnter += delegate(object s, MouseEventArgs _)
		{
			if (s is Border border4)
			{
				border4.Background = CardHoverBrush;
				border4.Effect = SharedCardShadow;
			}
		};
		border.MouseLeave += delegate(object s, MouseEventArgs _)
		{
			if (s is Border border3)
			{
				border3.Background = CardBgBrush;
				border3.Effect = null;
			}
		};
		VersionList.Children.Add(border);
		ApplyEnterAnimation(border, _listEnterMode, _staggerIndex);
		_staggerIndex++;
	}

	private async Task LoadImageAsync(Border border, string url)
	{
		Border border2 = border;
		string url2 = url;
		try
		{
			BitmapImage cachedBitmap = DataCache.Get<BitmapImage>(new object[2] { "img", url2 });
			if (cachedBitmap != null)
			{
				base.Dispatcher.Invoke(delegate
				{
					if (!(border2.Tag?.ToString() == "loaded"))
					{
						border2.Tag = "loaded";
						border2.Background = Brushes.Transparent;
						border2.Child = new Image
						{
							Source = cachedBitmap,
							Stretch = Stretch.UniformToFill
						};
					}
				});
				return;
			}
			using MemoryStream ms = new MemoryStream(await Task.Run(async () => await SharedImageClient.GetByteArrayAsync(url2)));
			BitmapImage bitmap = new BitmapImage();
			bitmap.BeginInit();
			bitmap.CacheOption = BitmapCacheOption.OnLoad;
			bitmap.CreateOptions = BitmapCreateOptions.IgnoreColorProfile;
			bitmap.StreamSource = ms;
			bitmap.EndInit();
			bitmap.Freeze();
			DataCache.Set(bitmap, "img", url2);
			base.Dispatcher.Invoke(delegate
			{
				if (!(border2.Tag?.ToString() == "loaded"))
				{
					border2.Tag = "loaded";
					border2.Background = Brushes.Transparent;
					Image child = new Image
					{
						Source = bitmap,
						Stretch = Stretch.UniformToFill
					};
					border2.Child = child;
				}
			});
		}
		catch (Exception)
		{
			base.Dispatcher.Invoke(delegate
			{
				border2.Tag = "failed";
				border2.Background = new SolidColorBrush(Color.FromRgb(55, 55, 58));
				border2.Child = new TextBlock
				{
					Text = "?",
					FontSize = 20.0,
					Foreground = new SolidColorBrush(Color.FromRgb(120, 120, 120)),
					HorizontalAlignment = HorizontalAlignment.Center,
					VerticalAlignment = VerticalAlignment.Center
				};
			});
		}
	}

	private void UpdateDownloadTaskList()
	{
		DownloadTask currentTask = DownloadManager.CurrentTask;
		if (currentTask == null)
		{
			if (_taskCard != null || DownloadTaskList.Children.Count == 0)
			{
				DownloadTaskList.Children.Clear();
				_taskCard = null;
				_taskCardProgress = null;
				_taskCardSpeed = null;
				_taskCardStep = null;
				_taskCardStatus = null;
				_taskCardTitle = null;
				_lastTaskStep = DownloadStep.Idle;
				_lastTaskName = "";
				Border border = new Border
				{
					Background = new SolidColorBrush(Color.FromRgb(42, 42, 46)),
					CornerRadius = new CornerRadius(8.0),
					Padding = new Thickness(20.0, 40.0, 20.0, 40.0),
					Margin = new Thickness(0.0, 20.0, 0.0, 0.0)
				};
				StackPanel stackPanel = new StackPanel
				{
					HorizontalAlignment = HorizontalAlignment.Center
				};
				TextBlock element = new TextBlock
				{
					Text = "○",
					FontSize = 32.0,
					Foreground = new SolidColorBrush(Color.FromRgb(80, 80, 85)),
					HorizontalAlignment = HorizontalAlignment.Center,
					Margin = new Thickness(0.0, 0.0, 0.0, 8.0)
				};
				TextBlock textBlock = new TextBlock
				{
					FontSize = 14.0,
					Foreground = new SolidColorBrush(Color.FromRgb(136, 136, 136)),
					HorizontalAlignment = HorizontalAlignment.Center
				};
				textBlock.SetResourceReference(TextBlock.TextProperty, "StatusNoDownloadTask");
				TextBlock textBlock2 = new TextBlock
				{
					FontSize = 11.0,
					Foreground = new SolidColorBrush(Color.FromRgb(102, 102, 102)),
					HorizontalAlignment = HorizontalAlignment.Center,
					Margin = new Thickness(0.0, 4.0, 0.0, 0.0)
				};
				textBlock2.SetResourceReference(TextBlock.TextProperty, "StatusClickDownloadStart");
				stackPanel.Children.Add(element);
				stackPanel.Children.Add(textBlock);
				stackPanel.Children.Add(textBlock2);
				border.Child = stackPanel;
				DownloadTaskList.Children.Add(border);
			}
		}
		else if (_taskCard == null || currentTask.Step != _lastTaskStep || currentTask.Name != _lastTaskName)
		{
			DownloadTaskList.Children.Clear();
			CreateDownloadTaskCard(currentTask);
			_lastTaskStep = currentTask.Step;
			_lastTaskName = currentTask.Name;
		}
		else
		{
			DateTime now = DateTime.Now;
			if (!((now - _lastCardUpdate).TotalMilliseconds < 150.0))
			{
				_lastCardUpdate = now;
				_taskCardProgress.Value = currentTask.Progress;
				_taskCardSpeed.Text = FormatSpeed(currentTask.Speed);
				_taskCardStep.Text = currentTask.StepText;
				string text = ((currentTask.Step == DownloadStep.Completed) ? LanguageManager.Get("DlStatusCompleted") : ((currentTask.Step == DownloadStep.Failed) ? LanguageManager.Get("DlStatusFailed") : ((currentTask.Step == DownloadStep.Cancelled) ? LanguageManager.Get("DlStatusCancelled") : LanguageManager.Get("DlStatusDownloading"))));
				Color color = ((currentTask.Step == DownloadStep.Completed) ? Color.FromRgb(100, 200, 100) : ((currentTask.Step == DownloadStep.Failed) ? Color.FromRgb(byte.MaxValue, 100, 100) : ((currentTask.Step == DownloadStep.Cancelled) ? Color.FromRgb(170, 170, 170) : Color.FromRgb(72, 144, 245))));
				_taskCardStatus.Text = text;
				_taskCardStatus.Foreground = new SolidColorBrush(color);
			}
		}
	}

	private void CreateDownloadTaskCard(DownloadTask task)
	{
		Border border = new Border
		{
			Background = new SolidColorBrush(Color.FromRgb(42, 42, 46)),
			CornerRadius = new CornerRadius(8.0),
			Padding = new Thickness(18.0, 14.0, 18.0, 14.0),
			Margin = new Thickness(0.0, 0.0, 0.0, 8.0)
		};
		StackPanel stackPanel = new StackPanel();
		Grid grid = new Grid();
		grid.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = new GridLength(1.0, GridUnitType.Star)
		});
		grid.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = GridLength.Auto
		});
		grid.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = GridLength.Auto
		});
		TextBlock textBlock = new TextBlock
		{
			Text = task.Name,
			Foreground = Brushes.White,
			FontSize = 14.0,
			FontWeight = FontWeights.SemiBold,
			VerticalAlignment = VerticalAlignment.Center
		};
		Grid.SetColumn(textBlock, 0);
		grid.Children.Add(textBlock);
		bool flag = task.Step != DownloadStep.Completed && task.Step != DownloadStep.Failed && task.Step != DownloadStep.Cancelled;
		if (flag)
		{
			Button button = new Button
			{
				Style = (Style)FindResource("CardButton"),
				Height = 28.0,
				FontSize = 12.0,
				Margin = new Thickness(12.0, 0.0, 0.0, 0.0),
				VerticalAlignment = VerticalAlignment.Center
			};
			button.Click += delegate
			{
				CancelDownload_Click();
			};
			button.SetResourceReference(ContentControl.ContentProperty, "CommonCancel");
			Grid.SetColumn(button, 1);
			grid.Children.Add(button);
		}
		string text = ((task.Step == DownloadStep.Completed) ? LanguageManager.Get("DlStatusCompleted") : ((task.Step == DownloadStep.Failed) ? LanguageManager.Get("DlStatusFailed") : ((task.Step == DownloadStep.Cancelled) ? LanguageManager.Get("DlStatusCancelled") : LanguageManager.Get("DlStatusDownloading"))));
		Color color = ((task.Step == DownloadStep.Completed) ? Color.FromRgb(100, 200, 100) : ((task.Step == DownloadStep.Failed) ? Color.FromRgb(byte.MaxValue, 100, 100) : ((task.Step == DownloadStep.Cancelled) ? Color.FromRgb(170, 170, 170) : Color.FromRgb(72, 144, 245))));
		TextBlock textBlock2 = new TextBlock
		{
			Text = text,
			Foreground = new SolidColorBrush(color),
			FontSize = 12.0,
			VerticalAlignment = VerticalAlignment.Center,
			HorizontalAlignment = HorizontalAlignment.Right,
			Margin = (flag ? new Thickness(10.0, 0.0, 0.0, 0.0) : new Thickness(0.0))
		};
		Grid.SetColumn(textBlock2, (!flag) ? 1 : 2);
		grid.Children.Add(textBlock2);
		stackPanel.Children.Add(grid);
		DockPanel dockPanel = new DockPanel
		{
			Margin = new Thickness(0.0, 10.0, 0.0, 0.0),
			LastChildFill = true
		};
		TextBlock textBlock3 = new TextBlock
		{
			Text = FormatSpeed(task.Speed),
			Foreground = new SolidColorBrush(Color.FromRgb(170, 170, 170)),
			FontSize = 11.0,
			Margin = new Thickness(10.0, 0.0, 0.0, 0.0),
			VerticalAlignment = VerticalAlignment.Center,
			Width = 80.0,
			TextAlignment = TextAlignment.Right
		};
		DockPanel.SetDock(textBlock3, Dock.Right);
		dockPanel.Children.Add(textBlock3);
		ProgressBar progressBar = new ProgressBar
		{
			Height = 4.0,
			Minimum = 0.0,
			Maximum = 100.0,
			Value = task.Progress,
			Background = new SolidColorBrush(Color.FromRgb(60, 60, 65)),
			Foreground = new SolidColorBrush(Color.FromRgb(72, 144, 245)),
			BorderThickness = new Thickness(0.0),
			VerticalAlignment = VerticalAlignment.Center
		};
		dockPanel.Children.Add(progressBar);
		stackPanel.Children.Add(dockPanel);
		TextBlock textBlock4 = new TextBlock
		{
			Text = task.StepText,
			Foreground = new SolidColorBrush(Color.FromRgb(153, 153, 153)),
			FontSize = 12.0,
			Margin = new Thickness(0.0, 8.0, 0.0, 0.0)
		};
		stackPanel.Children.Add(textBlock4);
		border.Child = stackPanel;
		DownloadTaskList.Children.Add(border);
		_taskCard = border;
		_taskCardProgress = progressBar;
		_taskCardSpeed = textBlock3;
		_taskCardStep = textBlock4;
		_taskCardStatus = textBlock2;
		_taskCardTitle = textBlock;
	}

	private string FormatSpeed(double bytesPerSecond)
	{
		if (!(bytesPerSecond < 1024.0))
		{
			if (!(bytesPerSecond < 1048576.0))
			{
				return $"{bytesPerSecond / 1048576.0:F1} MB/s";
			}
			return $"{bytesPerSecond / 1024.0:F1} KB/s";
		}
		return $"{(int)bytesPerSecond} B/s";
	}

	private void CancelDownload_Click()
	{
		DownloadManager.CancelDownload();
		UpdateDownloadTaskList();
	}

	private void OnDownloadProgressChanged(DownloadTask task)
	{
		base.Dispatcher.BeginInvoke((Action)delegate
		{
			if (DownloadCenterPage.Visibility == Visibility.Visible)
			{
				UpdateDownloadTaskList();
			}
		}, DispatcherPriority.Background);
	}

	private void OnDownloadCompleted(DownloadTask task)
	{
		base.Dispatcher.BeginInvoke((Action)delegate
		{
			if (DownloadCenterPage.Visibility == Visibility.Visible)
			{
				UpdateDownloadTaskList();
			}
		}, DispatcherPriority.Background);
	}

	private void OnDownloadFailed(DownloadTask task, Exception ex)
	{
		base.Dispatcher.BeginInvoke((Action)delegate
		{
			if (DownloadCenterPage.Visibility == Visibility.Visible)
			{
				UpdateDownloadTaskList();
			}
		}, DispatcherPriority.Background);
	}

	private void SmoothScroll_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
	{
		if (sender is ScrollViewer scrollViewer)
		{
			e.Handled = true;
			double value;
			double num = (_scrollTargets.TryGetValue(scrollViewer, out value) ? value : scrollViewer.VerticalOffset);
			double num2 = ((e.Delta > 0) ? (-90.0) : 90.0);
			double value2 = Math.Max(0.0, Math.Min(num + num2, scrollViewer.ScrollableHeight));
			_scrollStartOffsets[scrollViewer] = scrollViewer.VerticalOffset;
			_scrollTargets[scrollViewer] = value2;
			_scrollStartTimes[scrollViewer] = DateTime.Now;
			if (!_scrolling)
			{
				_scrolling = true;
				CompositionTarget.Rendering += SmoothScroll_Tick;
			}
		}
	}

	private void SmoothScroll_Tick(object? sender, EventArgs e)
	{
		bool flag = false;
		foreach (ScrollViewer item in _scrollStartTimes.Keys.ToList())
		{
			if (_scrollStartOffsets.TryGetValue(item, out var value) && _scrollTargets.TryGetValue(item, out var value2))
			{
				double totalMilliseconds = (DateTime.Now - _scrollStartTimes[item]).TotalMilliseconds;
				double num = Math.Min(totalMilliseconds / 350.0, 1.0);
				double num2 = 1.0 - Math.Pow(1.0 - num, 3.0);
				double offset = value + (value2 - value) * num2;
				item.ScrollToVerticalOffset(offset);
				if (num >= 1.0)
				{
					item.ScrollToVerticalOffset(value2);
					_scrollStartTimes.Remove(item);
					_scrollStartOffsets.Remove(item);
					_scrollTargets.Remove(item);
				}
				else
				{
					flag = true;
				}
			}
		}
		if (!flag)
		{
			_scrolling = false;
			CompositionTarget.Rendering -= SmoothScroll_Tick;
		}
	}

	private async void LoadVersionList()
	{
		List<(string id, string type)> cached = DataCache.Get<List<(string, string)>>(new object[1] { "versions" });
		if (cached != null && cached.Count > 0)
		{
			_allVersions.Clear();
			_allVersions.AddRange(cached);
			_listEnterMode = 2;
			_staggerIndex = 0;
			RefreshVersionList();
		}
		else
		{
			VersionList.Children.Clear();
			TextBlock loadingText = new TextBlock
			{
				Foreground = new SolidColorBrush(Color.FromRgb(136, 136, 136)),
				FontSize = 13.0,
				Margin = new Thickness(12.0, 20.0, 12.0, 0.0),
				HorizontalAlignment = HorizontalAlignment.Center
			};
			loadingText.SetResourceReference(TextBlock.TextProperty, "StatusLoadingVersionList");
			VersionList.Children.Add(loadingText);
		}
		try
		{
			List<(string id, string type)> result = await Task.Run(async delegate
			{
				using HttpClient client = new HttpClient
				{
					Timeout = TimeSpan.FromSeconds(15L)
				};
				string url = "https://launchermeta.mojang.com/mc/game/version_manifest.json";
				JsonDocument jsonDoc = JsonDocument.Parse(await client.GetStringAsync(url));
				JsonElement versions = jsonDoc.RootElement.GetProperty("versions");
				List<(string id, string type)> list = new List<(string, string)>();
				foreach (JsonElement version in versions.EnumerateArray())
				{
					string id = version.GetProperty("id").GetString() ?? "unknown";
					string type = version.GetProperty("type").GetString() ?? "unknown";
					list.Add((id, type));
				}
				return list;
			});
			DataCache.Set(result, "versions");
			_allVersions.Clear();
			_allVersions.AddRange(result);
			_listEnterMode = 2;
			_staggerIndex = 0;
			RefreshVersionList();
		}
		catch (Exception ex)
		{
			if (_allVersions.Count == 0)
			{
				VersionList.Children.Clear();
				TextBlock errorText = new TextBlock
				{
					Text = string.Format(LanguageManager.Get("ResLoadFailed"), ex.Message),
					Foreground = new SolidColorBrush(Color.FromRgb(245, 80, 80)),
					FontSize = 13.0,
					Margin = new Thickness(12.0, 20.0, 12.0, 0.0),
					HorizontalAlignment = HorizontalAlignment.Center
				};
				VersionList.Children.Add(errorText);
			}
		}
	}

	private void CreateVersionItem(string versionId, string versionType)
	{
		string versionId2 = versionId;
		StackPanel stackPanel = new StackPanel
		{
			Margin = new Thickness(0.0, 0.0, 0.0, 8.0)
		};
		Border headerBorder = new Border
		{
			Height = 48.0,
			Padding = new Thickness(16.0, 0.0, 16.0, 0.0),
			Background = VersionCardBgBrush,
			BorderBrush = VersionCardBorderBrush,
			BorderThickness = new Thickness(1.0),
			CornerRadius = new CornerRadius(6.0),
			Cursor = Cursors.Hand,
			Tag = versionId2
		};
		Grid grid = new Grid();
		grid.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = new GridLength(1.0, GridUnitType.Star)
		});
		grid.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = new GridLength(1.0, GridUnitType.Auto)
		});
		StackPanel stackPanel2 = new StackPanel
		{
			Orientation = Orientation.Horizontal,
			VerticalAlignment = VerticalAlignment.Center
		};
		TextBlock nameText = new TextBlock
		{
			Text = versionId2,
			Foreground = TextWhiteBrush,
			FontSize = 14.0,
			FontWeight = FontWeights.Medium,
			VerticalAlignment = VerticalAlignment.Center
		};
		TextBlock typeText = new TextBlock
		{
			Text = ((versionType == "release") ? LanguageManager.Get("ResTypeRelease") : LanguageManager.Get("ResTypeSnapshot")),
			Foreground = TextGrayBrush,
			FontSize = 11.0,
			Margin = new Thickness(10.0, 0.0, 0.0, 0.0),
			VerticalAlignment = VerticalAlignment.Center
		};
		stackPanel2.Children.Add(nameText);
		stackPanel2.Children.Add(typeText);
		Grid.SetColumn(stackPanel2, 0);
		grid.Children.Add(stackPanel2);
		TextBlock arrowText = new TextBlock
		{
			Text = "▼",
			Foreground = TextGrayBrush,
			FontSize = 12.0,
			VerticalAlignment = VerticalAlignment.Center,
			HorizontalAlignment = HorizontalAlignment.Right,
			RenderTransformOrigin = new Point(0.5, 0.5),
			RenderTransform = new RotateTransform(0.0)
		};
		Grid.SetColumn(arrowText, 1);
		grid.Children.Add(arrowText);
		headerBorder.Child = grid;
		stackPanel.Children.Add(headerBorder);
		StackPanel expandPanel = new StackPanel
		{
			Margin = new Thickness(0.0, 2.0, 0.0, 0.0),
			Opacity = 0.0,
			Height = 0.0,
			Visibility = Visibility.Collapsed
		};
		stackPanel.Children.Add(expandPanel);
		bool isExpanded = false;
		headerBorder.MouseEnter += delegate
		{
			if (!isExpanded)
			{
				headerBorder.Background = VersionCardHoverBrush;
			}
		};
		headerBorder.MouseLeave += delegate
		{
			if (!isExpanded)
			{
				headerBorder.Background = VersionCardBgBrush;
			}
		};
		headerBorder.MouseLeftButtonUp += delegate
		{
			isExpanded = !isExpanded;
			if (isExpanded)
			{
				headerBorder.Background = CardSelectedBrush;
				nameText.Foreground = TextWhiteBrush;
				typeText.Foreground = TextWhiteBrush;
				DoubleAnimation animation = new DoubleAnimation(180.0, TimeSpan.FromMilliseconds(250L, 0L))
				{
					EasingFunction = new CubicEase
					{
						EasingMode = EasingMode.EaseOut
					}
				};
				if (arrowText.RenderTransform is RotateTransform rotateTransform)
				{
					rotateTransform.BeginAnimation(RotateTransform.AngleProperty, animation);
				}
				if (expandPanel.Children.Count == 0)
				{
					CreateModLoaderSection(expandPanel, versionId2);
				}
				expandPanel.Visibility = Visibility.Visible;
				DoubleAnimation animation2 = new DoubleAnimation(210.0, TimeSpan.FromMilliseconds(350L, 0L))
				{
					EasingFunction = new CubicEase
					{
						EasingMode = EasingMode.EaseOut
					}
				};
				expandPanel.BeginAnimation(FrameworkElement.HeightProperty, animation2);
				DoubleAnimation animation3 = new DoubleAnimation(1.0, TimeSpan.FromMilliseconds(300L, 0L))
				{
					BeginTime = TimeSpan.FromMilliseconds(100L, 0L),
					EasingFunction = new CubicEase
					{
						EasingMode = EasingMode.EaseOut
					}
				};
				expandPanel.BeginAnimation(UIElement.OpacityProperty, animation3);
				SelectedVersionText.Text = versionId2;
				_selectedVersionId = versionId2;
				_selectedLoaderName = null;
				_selectedLoaderVersion = null;
				DownloadButton.IsEnabled = true;
			}
			else
			{
				headerBorder.Background = VersionCardBgBrush;
				nameText.Foreground = TextWhiteBrush;
				DoubleAnimation animation4 = new DoubleAnimation(0.0, TimeSpan.FromMilliseconds(250L, 0L))
				{
					EasingFunction = new CubicEase
					{
						EasingMode = EasingMode.EaseIn
					}
				};
				if (arrowText.RenderTransform is RotateTransform rotateTransform2)
				{
					rotateTransform2.BeginAnimation(RotateTransform.AngleProperty, animation4);
				}
				DoubleAnimation doubleAnimation = new DoubleAnimation(0.0, TimeSpan.FromMilliseconds(300L, 0L))
				{
					EasingFunction = new CubicEase
					{
						EasingMode = EasingMode.EaseIn
					}
				};
				doubleAnimation.Completed += delegate
				{
					expandPanel.Visibility = Visibility.Collapsed;
				};
				expandPanel.BeginAnimation(FrameworkElement.HeightProperty, doubleAnimation);
				DoubleAnimation animation5 = new DoubleAnimation(0.0, TimeSpan.FromMilliseconds(200L, 0L));
				expandPanel.BeginAnimation(UIElement.OpacityProperty, animation5);
			}
		};
		VersionList.Children.Add(stackPanel);
		ApplyEnterAnimation(stackPanel, _listEnterMode, _staggerIndex);
		_staggerIndex++;
	}

	private void CreateModLoaderSection(StackPanel parent, string gameVersion)
	{
		StackPanel parent2 = parent;
		string gameVersion2 = gameVersion;
		string[] array = new string[4] { "Forge", "Fabric", "NeoForge", "Quilt" };
		Border selectedLoader = null;
		StackPanel versionListPanel = null;
		string[] array2 = array;
		foreach (string loader in array2)
		{
			Border loaderBorder = new Border
			{
				Height = 44.0,
				Margin = new Thickness(0.0, 2.0, 0.0, 2.0),
				Padding = new Thickness(16.0, 0.0, 16.0, 0.0),
				Background = new SolidColorBrush(Color.FromRgb(37, 37, 38)),
				BorderBrush = new SolidColorBrush(Color.FromRgb(60, 60, 64)),
				BorderThickness = new Thickness(1.0),
				CornerRadius = new CornerRadius(6.0),
				Cursor = Cursors.Hand
			};
			Grid grid = new Grid();
			grid.ColumnDefinitions.Add(new ColumnDefinition
			{
				Width = new GridLength(1.0, GridUnitType.Auto)
			});
			grid.ColumnDefinitions.Add(new ColumnDefinition
			{
				Width = new GridLength(1.0, GridUnitType.Star)
			});
			grid.ColumnDefinitions.Add(new ColumnDefinition
			{
				Width = new GridLength(1.0, GridUnitType.Auto)
			});
			TextBlock crossText = new TextBlock
			{
				Text = "✕",
				Foreground = new SolidColorBrush(Color.FromRgb(72, 144, 245)),
				FontSize = 14.0,
				FontWeight = FontWeights.Bold,
				VerticalAlignment = VerticalAlignment.Center,
				Margin = new Thickness(0.0, 0.0, 12.0, 0.0),
				Visibility = Visibility.Collapsed
			};
			Grid.SetColumn(crossText, 0);
			grid.Children.Add(crossText);
			TextBlock loaderName = new TextBlock
			{
				Text = loader,
				Foreground = new SolidColorBrush(Color.FromRgb(220, 220, 220)),
				FontSize = 13.0,
				FontWeight = FontWeights.Medium,
				VerticalAlignment = VerticalAlignment.Center
			};
			Grid.SetColumn(loaderName, 1);
			grid.Children.Add(loaderName);
			TextBlock loaderArrow = new TextBlock
			{
				Text = "▼",
				Foreground = new SolidColorBrush(Color.FromRgb(120, 120, 120)),
				FontSize = 10.0,
				VerticalAlignment = VerticalAlignment.Center,
				HorizontalAlignment = HorizontalAlignment.Right,
				RenderTransformOrigin = new Point(0.5, 0.5),
				RenderTransform = new RotateTransform(0.0)
			};
			Grid.SetColumn(loaderArrow, 2);
			grid.Children.Add(loaderArrow);
			loaderBorder.Child = grid;
			parent2.Children.Add(loaderBorder);
			StackPanel loaderVersionPanel = new StackPanel
			{
				Margin = new Thickness(16.0, 2.0, 0.0, 4.0),
				Opacity = 0.0,
				Height = 0.0,
				Visibility = Visibility.Collapsed
			};
			parent2.Children.Add(loaderVersionPanel);
			loaderBorder.MouseEnter += delegate
			{
				if (selectedLoader != loaderBorder)
				{
					loaderBorder.Background = new SolidColorBrush(Color.FromRgb(50, 50, 52));
				}
			};
			loaderBorder.MouseLeave += delegate
			{
				if (selectedLoader != loaderBorder)
				{
					loaderBorder.Background = new SolidColorBrush(Color.FromRgb(37, 37, 38));
				}
			};
			loaderBorder.MouseLeftButtonUp += delegate
			{
				if (selectedLoader == loaderBorder)
				{
					crossText.Visibility = Visibility.Collapsed;
					loaderBorder.Background = new SolidColorBrush(Color.FromRgb(37, 37, 38));
					loaderBorder.BorderBrush = new SolidColorBrush(Color.FromRgb(60, 60, 64));
					loaderName.Foreground = new SolidColorBrush(Color.FromRgb(220, 220, 220));
					DoubleAnimation animation = new DoubleAnimation(0.0, TimeSpan.FromMilliseconds(200L, 0L));
					if (loaderArrow.RenderTransform is RotateTransform rotateTransform)
					{
						rotateTransform.BeginAnimation(RotateTransform.AngleProperty, animation);
					}
					DoubleAnimation doubleAnimation = new DoubleAnimation(0.0, TimeSpan.FromMilliseconds(250L, 0L))
					{
						EasingFunction = new CubicEase
						{
							EasingMode = EasingMode.EaseIn
						}
					};
					doubleAnimation.Completed += delegate
					{
						loaderVersionPanel.Visibility = Visibility.Collapsed;
					};
					loaderVersionPanel.BeginAnimation(FrameworkElement.HeightProperty, doubleAnimation);
					loaderVersionPanel.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0.0, TimeSpan.FromMilliseconds(150L, 0L)));
					DoubleAnimation animation2 = new DoubleAnimation(210.0, TimeSpan.FromMilliseconds(300L, 0L))
					{
						EasingFunction = new CubicEase
						{
							EasingMode = EasingMode.EaseIn
						}
					};
					parent2.BeginAnimation(FrameworkElement.HeightProperty, animation2);
					selectedLoader = null;
				}
				else
				{
					if (selectedLoader != null)
					{
						DeselectLoader(selectedLoader, parent2);
					}
					crossText.Visibility = Visibility.Visible;
					loaderBorder.Background = new SolidColorBrush(Color.FromRgb(40, 60, 100));
					loaderBorder.BorderBrush = new SolidColorBrush(Color.FromRgb(72, 144, 245));
					loaderName.Foreground = Brushes.White;
					DoubleAnimation animation3 = new DoubleAnimation(180.0, TimeSpan.FromMilliseconds(250L, 0L))
					{
						EasingFunction = new CubicEase
						{
							EasingMode = EasingMode.EaseOut
						}
					};
					if (loaderArrow.RenderTransform is RotateTransform rotateTransform2)
					{
						rotateTransform2.BeginAnimation(RotateTransform.AngleProperty, animation3);
					}
					if (loaderVersionPanel.Children.Count == 0)
					{
						CreateLoaderVersionItems(loaderVersionPanel, loader, gameVersion2);
					}
					loaderVersionPanel.Visibility = Visibility.Visible;
					DoubleAnimation animation4 = new DoubleAnimation(200.0, TimeSpan.FromMilliseconds(300L, 0L))
					{
						EasingFunction = new CubicEase
						{
							EasingMode = EasingMode.EaseOut
						}
					};
					loaderVersionPanel.BeginAnimation(FrameworkElement.HeightProperty, animation4);
					loaderVersionPanel.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(1.0, TimeSpan.FromMilliseconds(250L, 0L))
					{
						BeginTime = TimeSpan.FromMilliseconds(80L, 0L)
					});
					DoubleAnimation animation5 = new DoubleAnimation(420.0, TimeSpan.FromMilliseconds(350L, 0L))
					{
						EasingFunction = new CubicEase
						{
							EasingMode = EasingMode.EaseOut
						}
					};
					parent2.BeginAnimation(FrameworkElement.HeightProperty, animation5);
					selectedLoader = loaderBorder;
					versionListPanel = loaderVersionPanel;
				}
			};
		}
	}

	private void DeselectLoader(Border loaderBorder, StackPanel parent)
	{
		for (int i = 0; i < parent.Children.Count; i++)
		{
			if (parent.Children[i] != loaderBorder || !(loaderBorder.Child is Grid grid))
			{
				continue;
			}
			foreach (object child in grid.Children)
			{
				if (child is TextBlock textBlock)
				{
					if (textBlock.Text == "✕")
					{
						textBlock.Visibility = Visibility.Collapsed;
					}
					if (textBlock.Text == "Forge" || textBlock.Text == "Fabric" || textBlock.Text == "NeoForge" || textBlock.Text == "Quilt")
					{
						textBlock.Foreground = new SolidColorBrush(Color.FromRgb(220, 220, 220));
					}
					if (textBlock.Text == "▼" && textBlock.RenderTransform is RotateTransform rotateTransform)
					{
						DoubleAnimation animation = new DoubleAnimation(0.0, TimeSpan.FromMilliseconds(200L, 0L));
						rotateTransform.BeginAnimation(RotateTransform.AngleProperty, animation);
					}
				}
			}
			loaderBorder.Background = new SolidColorBrush(Color.FromRgb(37, 37, 38));
			loaderBorder.BorderBrush = new SolidColorBrush(Color.FromRgb(60, 60, 64));
			if (i + 1 < parent.Children.Count)
			{
				UIElement uIElement = parent.Children[i + 1];
				StackPanel versionPanel = uIElement as StackPanel;
				if (versionPanel != null)
				{
					DoubleAnimation doubleAnimation = new DoubleAnimation(0.0, TimeSpan.FromMilliseconds(250L, 0L))
					{
						EasingFunction = new CubicEase
						{
							EasingMode = EasingMode.EaseIn
						}
					};
					doubleAnimation.Completed += delegate
					{
						versionPanel.Visibility = Visibility.Collapsed;
					};
					versionPanel.BeginAnimation(FrameworkElement.HeightProperty, doubleAnimation);
					versionPanel.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0.0, TimeSpan.FromMilliseconds(150L, 0L)));
				}
			}
			DoubleAnimation animation2 = new DoubleAnimation(210.0, TimeSpan.FromMilliseconds(300L, 0L))
			{
				EasingFunction = new CubicEase
				{
					EasingMode = EasingMode.EaseIn
				}
			};
			parent.BeginAnimation(FrameworkElement.HeightProperty, animation2);
			break;
		}
	}

	private async void CreateLoaderVersionItems(StackPanel parent, string loaderName, string gameVersion)
	{
		string loaderName2 = loaderName;
		string gameVersion2 = gameVersion;
		string cacheKey = "loader_" + loaderName2 + "_" + gameVersion2;
		List<string> cachedVersions = DataCache.Get<List<string>>(new object[1] { cacheKey });
		if (cachedVersions != null && cachedVersions.Count > 0)
		{
			RenderLoaderVersions(parent, cachedVersions, loaderName2, gameVersion2);
		}
		else
		{
			parent.Children.Clear();
			TextBlock loadingText = new TextBlock
			{
				Foreground = new SolidColorBrush(Color.FromRgb(136, 136, 136)),
				FontSize = 12.0,
				Margin = new Thickness(12.0, 8.0, 0.0, 8.0)
			};
			loadingText.SetResourceReference(TextBlock.TextProperty, "StatusLoading");
			parent.Children.Add(loadingText);
		}
		List<string> versions = await Task.Run(() => DownloadManager.GetLoaderVersionsAsync(loaderName2, gameVersion2));
		DataCache.Set(versions, cacheKey);
		RenderLoaderVersions(parent, versions, loaderName2, gameVersion2);
	}

	private void RenderLoaderVersions(StackPanel parent, List<string> versions, string loaderName, string gameVersion)
	{
		string gameVersion2 = gameVersion;
		string loaderName2 = loaderName;
		parent.Children.Clear();
		if (versions == null || versions.Count == 0)
		{
			TextBlock textBlock = new TextBlock
			{
				Foreground = new SolidColorBrush(Color.FromRgb(136, 136, 136)),
				FontSize = 12.0,
				Margin = new Thickness(12.0, 8.0, 0.0, 8.0)
			};
			textBlock.SetResourceReference(TextBlock.TextProperty, "StatusNoAvailableVersion");
			parent.Children.Add(textBlock);
			return;
		}
		ScrollViewer scrollViewer = new ScrollViewer
		{
			VerticalScrollBarVisibility = ScrollBarVisibility.Hidden,
			HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
			Height = 190.0
		};
		scrollViewer.PreviewMouseWheel += SmoothScroll_PreviewMouseWheel;
		StackPanel stackPanel = new StackPanel();
		foreach (string version in versions)
		{
			Border item = new Border
			{
				Height = 36.0,
				Margin = new Thickness(0.0, 1.0, 0.0, 1.0),
				Padding = new Thickness(12.0, 0.0, 12.0, 0.0),
				Background = new SolidColorBrush(Color.FromRgb(30, 30, 32)),
				CornerRadius = new CornerRadius(4.0),
				Cursor = Cursors.Hand
			};
			TextBlock text = new TextBlock
			{
				Text = version,
				Foreground = new SolidColorBrush(Color.FromRgb(200, 200, 200)),
				FontSize = 12.0,
				VerticalAlignment = VerticalAlignment.Center
			};
			item.Child = text;
			item.MouseEnter += delegate
			{
				item.Background = new SolidColorBrush(Color.FromRgb(50, 50, 52));
				text.Foreground = Brushes.White;
			};
			item.MouseLeave += delegate
			{
				item.Background = new SolidColorBrush(Color.FromRgb(30, 30, 32));
				text.Foreground = new SolidColorBrush(Color.FromRgb(200, 200, 200));
			};
			string capturedVer = version;
			item.MouseLeftButtonUp += delegate
			{
				SelectedVersionText.Text = $"{gameVersion2} - {loaderName2} {capturedVer}";
				_selectedVersionId = gameVersion2;
				_selectedLoaderName = loaderName2;
				_selectedLoaderVersion = capturedVer;
				DownloadButton.IsEnabled = true;
			};
			stackPanel.Children.Add(item);
		}
		scrollViewer.Content = stackPanel;
		parent.Children.Add(scrollViewer);
	}

	private void DownloadCategory_Enter(object sender, MouseEventArgs e)
	{
		if (sender is Border { Child: TextBlock child } border)
		{
			border.Background = new SolidColorBrush(Color.FromRgb(62, 62, 66));
			child.Foreground = Brushes.White;
		}
	}

	private void DownloadCategory_Leave(object sender, MouseEventArgs e)
	{
		if (sender is Border { Child: TextBlock child } border)
		{
			border.Background = Brushes.Transparent;
			child.Foreground = new SolidColorBrush(Color.FromRgb(204, 204, 204));
		}
	}

	private void ResourceDownload_Click(object sender, RoutedEventArgs e)
	{
		if (sender is Button { Tag: ModrinthProject tag })
		{
			ShowVersionSelectPage(tag);
		}
	}

	private async void ShowVersionSelectPage(ModrinthProject proj)
	{
		ModrinthProject proj2 = proj;
		try
		{
			DownloadPage.Visibility = Visibility.Collapsed;
			DownloadCenterPage.Visibility = Visibility.Collapsed;
			ContentArea.Visibility = Visibility.Visible;
			string currentResourceType = _currentResourceType;
			if (1 == 0)
			{
			}
			ResourceType resourceType = currentResourceType switch
			{
				"模组" => ResourceType.Mod, 
				"光影" => ResourceType.Shader, 
				"材质" => ResourceType.ResourcePack, 
				_ => ResourceType.Mod, 
			};
			if (1 == 0)
			{
			}
			ResourceType resType = resourceType;
			_versionSelectOverlay = new Border
			{
				Background = new SolidColorBrush(Color.FromRgb(0, 0, 0)),
				Opacity = 0.0,
				HorizontalAlignment = HorizontalAlignment.Stretch,
				VerticalAlignment = VerticalAlignment.Stretch,
				Cursor = Cursors.Arrow
			};
			_versionSelectOverlay.MouseLeftButtonUp += delegate
			{
				CloseVersionSelectPage();
			};
			_versionSelectPanel = new Border
			{
				Background = new SolidColorBrush(Color.FromRgb(28, 28, 32)),
				CornerRadius = new CornerRadius(12.0),
				Width = 460.0,
				HorizontalAlignment = HorizontalAlignment.Right,
				VerticalAlignment = VerticalAlignment.Stretch,
				Margin = new Thickness(0.0, 0.0, 0.0, 0.0),
				ClipToBounds = true
			};
			_versionSelectPanel.RenderTransform = new TranslateTransform
			{
				X = 460.0
			};
			Grid root = new Grid
			{
				RowDefinitions = 
				{
					new RowDefinition
					{
						Height = GridLength.Auto
					},
					new RowDefinition
					{
						Height = new GridLength(1.0, GridUnitType.Star)
					}
				}
			};
			Grid header = BuildVersionSelectHeader(proj2);
			root.Children.Add(header);
			Grid.SetRow(header, 0);
			ScrollViewer bodyScroll = new ScrollViewer
			{
				VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
				HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
				Margin = new Thickness(0.0),
				Padding = new Thickness(20.0, 0.0, 20.0, 20.0)
			};
			bodyScroll.PreviewMouseWheel += SmoothScroll_PreviewMouseWheel;
			StackPanel bodyStack = new StackPanel();
			TextBlock loadingText = new TextBlock
			{
				Foreground = new SolidColorBrush(Color.FromRgb(140, 140, 140)),
				FontSize = 13.0,
				HorizontalAlignment = HorizontalAlignment.Center,
				Margin = new Thickness(0.0, 40.0, 0.0, 0.0)
			};
			loadingText.SetResourceReference(TextBlock.TextProperty, "StatusLoadingVersionList");
			bodyStack.Children.Add(loadingText);
			bodyScroll.Content = bodyStack;
			root.Children.Add(bodyScroll);
			Grid.SetRow(bodyScroll, 1);
			_versionSelectPanel.Child = root;
			ContentArea.Children.Add(_versionSelectOverlay);
			ContentArea.Children.Add(_versionSelectPanel);
			CubicEase ease = new CubicEase
			{
				EasingMode = EasingMode.EaseOut
			};
			TimeSpan dur = TimeSpan.FromMilliseconds(350L, 0L);
			_versionSelectOverlay.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0.6, dur)
			{
				EasingFunction = ease
			});
			((TranslateTransform)_versionSelectPanel.RenderTransform).BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation(0.0, dur)
			{
				EasingFunction = ease
			});
			List<ModrinthVersion> cachedVersions = DataCache.Get<List<ModrinthVersion>>(new object[2] { "project_versions", proj2.ProjectId });
			if (cachedVersions != null && cachedVersions.Count > 0)
			{
				RenderGroupedVersions(bodyStack, proj2, cachedVersions, resType);
			}
			List<ModrinthVersion> versions = await Task.Run(() => ModrinthApi.GetProjectVersions(proj2.ProjectId));
			DataCache.Set(versions, "project_versions", proj2.ProjectId);
			RenderGroupedVersions(bodyStack, proj2, versions, resType);
		}
		catch (Exception)
		{
		}
	}

	private Grid BuildVersionSelectHeader(ModrinthProject proj)
	{
		Grid grid = new Grid
		{
			Background = new SolidColorBrush(Color.FromRgb(24, 24, 28)),
			Height = 110.0,
			ClipToBounds = true
		};
		grid.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = GridLength.Auto
		});
		grid.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = new GridLength(1.0, GridUnitType.Star)
		});
		grid.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = GridLength.Auto
		});
		Border border = new Border
		{
			Width = 64.0,
			Height = 64.0,
			CornerRadius = new CornerRadius(12.0),
			Background = new SolidColorBrush(Color.FromRgb(55, 55, 58)),
			ClipToBounds = true,
			Margin = new Thickness(20.0, 0.0, 0.0, 0.0),
			VerticalAlignment = VerticalAlignment.Center
		};
		if (!string.IsNullOrEmpty(proj.IconUrl))
		{
			LoadImageAsync(border, proj.IconUrl);
		}
		else
		{
			border.Child = new TextBlock
			{
				Text = "?",
				FontSize = 24.0,
				Foreground = new SolidColorBrush(Color.FromRgb(120, 120, 120)),
				HorizontalAlignment = HorizontalAlignment.Center,
				VerticalAlignment = VerticalAlignment.Center
			};
		}
		Grid.SetColumn(border, 0);
		grid.Children.Add(border);
		StackPanel stackPanel = new StackPanel
		{
			Margin = new Thickness(14.0, 0.0, 0.0, 0.0),
			VerticalAlignment = VerticalAlignment.Center
		};
		TextBlock element = new TextBlock
		{
			Text = proj.Title,
			Foreground = Brushes.White,
			FontSize = 15.0,
			FontWeight = FontWeights.SemiBold,
			TextTrimming = TextTrimming.CharacterEllipsis
		};
		stackPanel.Children.Add(element);
		string text = (string.IsNullOrEmpty(proj.Description) ? LanguageManager.Get("ResNoDesc") : ((proj.Description.Length > 50) ? (proj.Description.Substring(0, 50) + "...") : proj.Description));
		TextBlock element2 = new TextBlock
		{
			Text = text,
			Foreground = new SolidColorBrush(Color.FromRgb(150, 150, 150)),
			FontSize = 11.0,
			Margin = new Thickness(0.0, 4.0, 0.0, 0.0),
			TextTrimming = TextTrimming.CharacterEllipsis,
			MaxWidth = 280.0
		};
		stackPanel.Children.Add(element2);
		Grid.SetColumn(stackPanel, 1);
		grid.Children.Add(stackPanel);
		return grid;
	}

	private void CloseVersionSelectPage()
	{
		if (_versionSelectPanel == null || _versionSelectOverlay == null)
		{
			return;
		}
		CubicEase easingFunction = new CubicEase
		{
			EasingMode = EasingMode.EaseIn
		};
		TimeSpan timeSpan = TimeSpan.FromMilliseconds(280L, 0L);
		DoubleAnimation doubleAnimation = new DoubleAnimation(460.0, timeSpan)
		{
			EasingFunction = easingFunction
		};
		DoubleAnimation animation = new DoubleAnimation(0.0, timeSpan)
		{
			EasingFunction = easingFunction
		};
		doubleAnimation.Completed += delegate
		{
			ContentArea.Children.Remove(_versionSelectOverlay);
			ContentArea.Children.Remove(_versionSelectPanel);
			_versionSelectOverlay = null;
			_versionSelectPanel = null;
			DownloadPage.Visibility = Visibility.Visible;
			ContentArea.Visibility = Visibility.Visible;
			if (_currentResourceType == "游戏")
			{
				GameCategoryBar.Visibility = Visibility.Visible;
				ResourceSearchBar.Visibility = Visibility.Collapsed;
				BottomActionBar.Visibility = Visibility.Visible;
				ListArea.CornerRadius = new CornerRadius(0.0);
				if (_allVersions.Count > 0)
				{
					RefreshVersionList();
				}
			}
			else
			{
				GameCategoryBar.Visibility = Visibility.Collapsed;
				ResourceSearchBar.Visibility = Visibility.Visible;
				BottomActionBar.Visibility = Visibility.Collapsed;
				ListArea.CornerRadius = new CornerRadius(0.0, 0.0, 8.0, 8.0);
				base.Dispatcher.BeginInvoke((Action)delegate
				{
					Focus();
					if (ResourceSearchBox != null)
					{
						ResourceSearchBox.Focus();
						Keyboard.Focus(ResourceSearchBox);
					}
				}, DispatcherPriority.ContextIdle);
			}
		};
		((TranslateTransform)_versionSelectPanel.RenderTransform).BeginAnimation(TranslateTransform.XProperty, doubleAnimation);
		_versionSelectOverlay.BeginAnimation(UIElement.OpacityProperty, animation);
	}

	private void RenderGroupedVersions(StackPanel bodyStack, ModrinthProject proj, List<ModrinthVersion> versions, ResourceType resType)
	{
		bodyStack.Children.Clear();
		if (versions.Count == 0)
		{
			TextBlock textBlock = new TextBlock
			{
				Foreground = new SolidColorBrush(Color.FromRgb(140, 140, 140)),
				FontSize = 13.0,
				HorizontalAlignment = HorizontalAlignment.Center,
				Margin = new Thickness(0.0, 40.0, 0.0, 0.0)
			};
			textBlock.SetResourceReference(TextBlock.TextProperty, "StatusNoAvailableVersion");
			bodyStack.Children.Add(textBlock);
			return;
		}
		var orderedEnumerable = (from x in versions.SelectMany((ModrinthVersion v) => v.GameVersions.Select((string gv) => new
			{
				Version = v,
				GameVersion = gv
			}))
			group x by x.GameVersion).OrderByDescending(g => g.Key, new VersionComparer());
		string[] loaderOrder = new string[6] { "fabric", "forge", "quilt", "neoforge", "iris", "optifine" };
		foreach (var item in orderedEnumerable)
		{
			var orderedEnumerable2 = from x in item.Select(x => x.Version).Distinct().SelectMany((ModrinthVersion v) => ((v.Loaders.Count == 0) ? ((IEnumerable<string>)new List<string> { "any" }) : ((IEnumerable<string>)v.Loaders)).Select((string l) => new
				{
					Version = v,
					Loader = l.ToLowerInvariant()
				}))
				group x by x.Loader into g
				orderby (Array.IndexOf(loaderOrder, g.Key) < 0) ? 99 : Array.IndexOf(loaderOrder, g.Key), g.Key
				select g;
			foreach (var item2 in orderedEnumerable2)
			{
				object obj;
				if (!(item2.Key == "any"))
				{
					if (!(item2.Key == "neoforge"))
					{
						char reference = char.ToUpper(item2.Key[0]);
						obj = string.Concat(new ReadOnlySpan<char>(ref reference), item2.Key.Substring(1));
					}
					else
					{
						obj = "NeoForge";
					}
				}
				else
				{
					obj = LanguageManager.Get("ResLoaderAny");
				}
				string text = (string)obj;
				List<ModrinthVersion> versions2 = item2.Select(x => x.Version).Distinct().ToList();
				Border element = CreateVersionDropdown(text + " · " + item.Key, versions2, proj, resType);
				bodyStack.Children.Add(element);
			}
		}
	}

	private Border CreateVersionDropdown(string title, List<ModrinthVersion> versions, ModrinthProject proj, ResourceType resType)
	{
		Border border = new Border
		{
			Background = new SolidColorBrush(Color.FromRgb(38, 38, 42)),
			CornerRadius = new CornerRadius(8.0),
			Margin = new Thickness(0.0, 0.0, 0.0, 8.0),
			ClipToBounds = true
		};
		StackPanel stackPanel = new StackPanel();
		Border border2 = new Border
		{
			Height = 40.0,
			Padding = new Thickness(14.0, 0.0, 14.0, 0.0),
			Background = new SolidColorBrush(Color.FromRgb(38, 38, 42)),
			Cursor = Cursors.Hand
		};
		Grid grid = new Grid();
		grid.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = new GridLength(1.0, GridUnitType.Star)
		});
		grid.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = GridLength.Auto
		});
		grid.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = GridLength.Auto
		});
		TextBlock element = new TextBlock
		{
			Text = title,
			Foreground = Brushes.White,
			FontSize = 13.0,
			FontWeight = FontWeights.Medium,
			VerticalAlignment = VerticalAlignment.Center
		};
		Grid.SetColumn(element, 0);
		grid.Children.Add(element);
		TextBlock element2 = new TextBlock
		{
			Text = string.Format(LanguageManager.Get("ResVersionCount"), versions.Count),
			Foreground = new SolidColorBrush(Color.FromRgb(130, 130, 130)),
			FontSize = 11.0,
			VerticalAlignment = VerticalAlignment.Center,
			Margin = new Thickness(0.0, 0.0, 10.0, 0.0)
		};
		Grid.SetColumn(element2, 1);
		grid.Children.Add(element2);
		TextBlock arrow = new TextBlock
		{
			Text = "▾",
			Foreground = new SolidColorBrush(Color.FromRgb(170, 170, 170)),
			FontSize = 12.0,
			VerticalAlignment = VerticalAlignment.Center,
			RenderTransformOrigin = new Point(0.5, 0.5),
			RenderTransform = new RotateTransform(0.0)
		};
		Grid.SetColumn(arrow, 2);
		grid.Children.Add(arrow);
		border2.Child = grid;
		StackPanel contentPanel = new StackPanel
		{
			Margin = new Thickness(0.0, 0.0, 0.0, 0.0)
		};
		contentPanel.RenderTransform = new ScaleTransform(1.0, 0.0);
		contentPanel.RenderTransformOrigin = new Point(0.5, 0.0);
		contentPanel.Visibility = Visibility.Collapsed;
		foreach (ModrinthVersion version in versions)
		{
			contentPanel.Children.Add(CreateVersionItem(version, proj, resType));
		}
		bool expanded = false;
		border2.MouseLeftButtonUp += delegate
		{
			expanded = !expanded;
			CubicEase easingFunction = new CubicEase
			{
				EasingMode = EasingMode.EaseOut
			};
			TimeSpan timeSpan = TimeSpan.FromMilliseconds(250L, 0L);
			if (expanded)
			{
				contentPanel.Visibility = Visibility.Visible;
				DoubleAnimation animation = new DoubleAnimation(0.0, 1.0, timeSpan)
				{
					EasingFunction = easingFunction
				};
				((ScaleTransform)contentPanel.RenderTransform).BeginAnimation(ScaleTransform.ScaleYProperty, animation);
				((RotateTransform)arrow.RenderTransform).BeginAnimation(RotateTransform.AngleProperty, new DoubleAnimation(180.0, timeSpan)
				{
					EasingFunction = easingFunction
				});
			}
			else
			{
				DoubleAnimation doubleAnimation = new DoubleAnimation(1.0, 0.0, timeSpan)
				{
					EasingFunction = easingFunction
				};
				doubleAnimation.Completed += delegate
				{
					contentPanel.Visibility = Visibility.Collapsed;
				};
				((ScaleTransform)contentPanel.RenderTransform).BeginAnimation(ScaleTransform.ScaleYProperty, doubleAnimation);
				((RotateTransform)arrow.RenderTransform).BeginAnimation(RotateTransform.AngleProperty, new DoubleAnimation(0.0, timeSpan)
				{
					EasingFunction = easingFunction
				});
			}
		};
		border2.MouseEnter += delegate(object s, MouseEventArgs _)
		{
			if (s is Border border4)
			{
				border4.Background = new SolidColorBrush(Color.FromRgb(48, 48, 52));
			}
		};
		border2.MouseLeave += delegate(object s, MouseEventArgs _)
		{
			if (s is Border border3)
			{
				border3.Background = new SolidColorBrush(Color.FromRgb(38, 38, 42));
			}
		};
		stackPanel.Children.Add(border2);
		stackPanel.Children.Add(contentPanel);
		border.Child = stackPanel;
		return border;
	}

	private Border CreateVersionItem(ModrinthVersion ver, ModrinthProject proj, ResourceType resType)
	{
		Border border = new Border
		{
			Background = new SolidColorBrush(Color.FromRgb(32, 32, 36)),
			CornerRadius = new CornerRadius(6.0),
			Padding = new Thickness(12.0, 8.0, 12.0, 8.0),
			Margin = new Thickness(8.0, 2.0, 8.0, 2.0),
			Cursor = Cursors.Hand
		};
		Grid grid = new Grid();
		grid.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = new GridLength(1.0, GridUnitType.Star)
		});
		grid.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = GridLength.Auto
		});
		StackPanel stackPanel = new StackPanel
		{
			VerticalAlignment = VerticalAlignment.Center
		};
		StackPanel stackPanel2 = new StackPanel
		{
			Orientation = Orientation.Horizontal
		};
		TextBlock element = new TextBlock
		{
			Text = (string.IsNullOrEmpty(ver.Name) ? ver.VersionNumber : ver.Name),
			Foreground = Brushes.White,
			FontSize = 12.0,
			FontWeight = FontWeights.Medium,
			TextTrimming = TextTrimming.CharacterEllipsis,
			MaxWidth = 240.0
		};
		stackPanel2.Children.Add(element);
		string versionType = ver.VersionType;
		if (1 == 0)
		{
		}
		Color color = ((versionType == "beta") ? Color.FromRgb(200, 160, 60) : ((!(versionType == "alpha")) ? Color.FromRgb(80, 200, 120) : Color.FromRgb(200, 100, 100)));
		if (1 == 0)
		{
		}
		Color color2 = color;
		Border element2 = new Border
		{
			Background = new SolidColorBrush(color2),
			CornerRadius = new CornerRadius(3.0),
			Padding = new Thickness(5.0, 0.0, 5.0, 0.0),
			Margin = new Thickness(8.0, 0.0, 0.0, 0.0),
			Child = new TextBlock
			{
				Text = ver.VersionType,
				Foreground = Brushes.White,
				FontSize = 9.0,
				VerticalAlignment = VerticalAlignment.Center
			}
		};
		stackPanel2.Children.Add(element2);
		stackPanel.Children.Add(stackPanel2);
		if (!string.IsNullOrEmpty(ver.DatePublished))
		{
			try
			{
				DateTime dateTime = DateTime.Parse(ver.DatePublished);
				stackPanel.Children.Add(new TextBlock
				{
					Text = dateTime.ToString("yyyy-MM-dd"),
					Foreground = new SolidColorBrush(Color.FromRgb(130, 130, 130)),
					FontSize = 10.0,
					Margin = new Thickness(0.0, 2.0, 0.0, 0.0)
				});
			}
			catch
			{
			}
		}
		Grid.SetColumn(stackPanel, 0);
		grid.Children.Add(stackPanel);
		Button dlBtn = new Button
		{
			Style = (Style)FindResource("CardButton"),
			Height = 26.0,
			FontSize = 11.0,
			VerticalAlignment = VerticalAlignment.Center,
			Tag = new
			{
				Project = proj,
				Version = ver,
				ResType = resType
			}
		};
		dlBtn.SetResourceReference(ContentControl.ContentProperty, "DownloadStart");
		dlBtn.Click += VersionItemDownload_Click;
		Grid.SetColumn(dlBtn, 1);
		grid.Children.Add(dlBtn);
		border.Child = grid;
		border.MouseEnter += delegate(object s, MouseEventArgs _)
		{
			if (s is Border border3)
			{
				border3.Background = new SolidColorBrush(Color.FromRgb(42, 42, 46));
			}
		};
		border.MouseLeave += delegate(object s, MouseEventArgs _)
		{
			if (s is Border border2)
			{
				border2.Background = new SolidColorBrush(Color.FromRgb(32, 32, 36));
			}
		};
		border.MouseLeftButtonUp += delegate
		{
			VersionItemDownload_Click(dlBtn, null);
		};
		return border;
	}

	private void VersionItemDownload_Click(object sender, RoutedEventArgs e)
	{
		if (sender is Button { Tag: not null, Tag: var tag })
		{
			ModrinthProject proj = (ModrinthProject)((dynamic)tag).Project;
			ModrinthVersion ver = (ModrinthVersion)((dynamic)tag).Version;
			ResourceType resType = (ResourceType)((dynamic)tag).ResType;
			ShowDownloadSourcePopup(proj, ver, resType);
		}
	}

	private void ShowDownloadSourcePopup(ModrinthProject proj, ModrinthVersion ver, ResourceType resType)
	{
		ModrinthProject proj2 = proj;
		ModrinthVersion ver2 = ver;
		if (_versionSelectPanel == null)
		{
			return;
		}
		Border overlay = new Border
		{
			Background = new SolidColorBrush(Color.FromArgb(180, 0, 0, 0)),
			HorizontalAlignment = HorizontalAlignment.Stretch,
			VerticalAlignment = VerticalAlignment.Stretch,
			Cursor = Cursors.Arrow
		};
		Border popup = new Border
		{
			Background = new SolidColorBrush(Color.FromRgb(32, 32, 36)),
			CornerRadius = new CornerRadius(12.0),
			Width = 340.0,
			Padding = new Thickness(20.0),
			HorizontalAlignment = HorizontalAlignment.Center,
			VerticalAlignment = VerticalAlignment.Center,
			RenderTransform = new ScaleTransform(0.9, 0.9),
			RenderTransformOrigin = new Point(0.5, 0.5)
		};
		StackPanel stackPanel = new StackPanel();
		TextBlock textBlock = new TextBlock
		{
			Foreground = Brushes.White,
			FontSize = 15.0,
			FontWeight = FontWeights.SemiBold,
			HorizontalAlignment = HorizontalAlignment.Center,
			Margin = new Thickness(0.0, 0.0, 0.0, 4.0)
		};
		textBlock.SetResourceReference(TextBlock.TextProperty, "StatusSelectDownloadUrl");
		stackPanel.Children.Add(textBlock);
		TextBlock element = new TextBlock
		{
			Text = (string.IsNullOrEmpty(ver2.Name) ? ver2.VersionNumber : ver2.Name),
			Foreground = new SolidColorBrush(Color.FromRgb(140, 140, 140)),
			FontSize = 11.0,
			HorizontalAlignment = HorizontalAlignment.Center,
			Margin = new Thickness(0.0, 0.0, 0.0, 16.0),
			TextTrimming = TextTrimming.CharacterEllipsis
		};
		stackPanel.Children.Add(element);
		var array = new[]
		{
			new
			{
				Name = LanguageManager.Get("MirrorOfficialName"),
				Desc = LanguageManager.Get("MirrorOfficialDesc"),
				Mirror = ""
			},
			new
			{
				Name = LanguageManager.Get("MirrorGithubName"),
				Desc = LanguageManager.Get("MirrorGithubDesc"),
				Mirror = "ghproxy"
			},
			new
			{
				Name = LanguageManager.Get("MirrorCustomName"),
				Desc = LanguageManager.Get("MirrorCustomDesc"),
				Mirror = "custom"
			}
		};
		TextBox customMirrorBox = null;
		var array2 = array;
		foreach (var anon in array2)
		{
			Border border = new Border
			{
				Background = new SolidColorBrush(Color.FromRgb(42, 42, 46)),
				CornerRadius = new CornerRadius(8.0),
				Padding = new Thickness(14.0, 10.0, 14.0, 10.0),
				Margin = new Thickness(0.0, 0.0, 0.0, 8.0),
				Cursor = Cursors.Hand,
				Tag = anon.Mirror
			};
			Grid grid = new Grid();
			grid.ColumnDefinitions.Add(new ColumnDefinition
			{
				Width = new GridLength(1.0, GridUnitType.Star)
			});
			grid.ColumnDefinitions.Add(new ColumnDefinition
			{
				Width = GridLength.Auto
			});
			StackPanel stackPanel2 = new StackPanel();
			stackPanel2.Children.Add(new TextBlock
			{
				Text = anon.Name,
				Foreground = Brushes.White,
				FontSize = 13.0,
				FontWeight = FontWeights.Medium
			});
			stackPanel2.Children.Add(new TextBlock
			{
				Text = anon.Desc,
				Foreground = new SolidColorBrush(Color.FromRgb(130, 130, 130)),
				FontSize = 10.0,
				Margin = new Thickness(0.0, 2.0, 0.0, 0.0)
			});
			Grid.SetColumn(stackPanel2, 0);
			grid.Children.Add(stackPanel2);
			TextBlock element2 = new TextBlock
			{
				Text = "→",
				Foreground = new SolidColorBrush(Color.FromRgb(130, 130, 130)),
				FontSize = 14.0,
				VerticalAlignment = VerticalAlignment.Center
			};
			Grid.SetColumn(element2, 1);
			grid.Children.Add(element2);
			border.Child = grid;
			string capturedMirror = anon.Mirror;
			border.MouseLeftButtonUp += delegate
			{
				if (capturedMirror == "custom")
				{
					string text = customMirrorBox?.Text?.Trim() ?? "";
					if (string.IsNullOrEmpty(text))
					{
						customMirrorBox?.Focus();
						return;
					}
					StartVersionDownload(proj2, ver2, resType, text);
				}
				else
				{
					StartVersionDownload(proj2, ver2, resType, capturedMirror);
				}
				CloseDownloadSourcePopup(overlay, popup);
			};
			border.MouseEnter += delegate(object s, MouseEventArgs _)
			{
				if (s is Border border3)
				{
					border3.Background = new SolidColorBrush(Color.FromRgb(52, 52, 56));
				}
			};
			border.MouseLeave += delegate(object s, MouseEventArgs _)
			{
				if (s is Border border2)
				{
					border2.Background = new SolidColorBrush(Color.FromRgb(42, 42, 46));
				}
			};
			stackPanel.Children.Add(border);
		}
		customMirrorBox = new TextBox
		{
			Background = new SolidColorBrush(Color.FromRgb(42, 42, 46)),
			Foreground = Brushes.White,
			FontSize = 12.0,
			BorderThickness = new Thickness(1.0),
			BorderBrush = new SolidColorBrush(Color.FromRgb(60, 60, 64)),
			Padding = new Thickness(10.0, 6.0, 10.0, 6.0),
			Margin = new Thickness(0.0, 4.0, 0.0, 0.0),
			Tag = "StatusCustomMirrorHint"
		};
		TextBlock placeholder = new TextBlock
		{
			Foreground = new SolidColorBrush(Color.FromRgb(100, 100, 100)),
			FontSize = 12.0,
			IsHitTestVisible = false,
			VerticalAlignment = VerticalAlignment.Center,
			Margin = new Thickness(10.0, 0.0, 0.0, 0.0)
		};
		placeholder.SetResourceReference(TextBlock.TextProperty, "StatusCustomMirrorHint");
		Grid grid2 = new Grid();
		grid2.Children.Add(customMirrorBox);
		grid2.Children.Add(placeholder);
		customMirrorBox.TextChanged += delegate
		{
			placeholder.Visibility = ((!string.IsNullOrEmpty(customMirrorBox.Text)) ? Visibility.Collapsed : Visibility.Visible);
		};
		stackPanel.Children.Add(grid2);
		Button button = new Button
		{
			Style = (Style)FindResource("CardButton"),
			Height = 34.0,
			FontSize = 12.0,
			Margin = new Thickness(0.0, 8.0, 0.0, 0.0),
			Background = new SolidColorBrush(Color.FromRgb(50, 50, 54)),
			Foreground = new SolidColorBrush(Color.FromRgb(170, 170, 170))
		};
		button.Click += delegate
		{
			CloseDownloadSourcePopup(overlay, popup);
		};
		button.SetResourceReference(ContentControl.ContentProperty, "CommonCancel");
		stackPanel.Children.Add(button);
		popup.Child = stackPanel;
		if (_versionSelectPanel.Child is Grid grid3)
		{
			grid3.Children.Add(overlay);
			grid3.Children.Add(popup);
		}
		CubicEase easingFunction = new CubicEase
		{
			EasingMode = EasingMode.EaseOut
		};
		TimeSpan timeSpan = TimeSpan.FromMilliseconds(200L, 0L);
		overlay.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0.0, 1.0, timeSpan)
		{
			EasingFunction = easingFunction
		});
		DoubleAnimation animation = new DoubleAnimation(0.9, 1.0, timeSpan)
		{
			EasingFunction = easingFunction
		};
		((ScaleTransform)popup.RenderTransform).BeginAnimation(ScaleTransform.ScaleXProperty, animation);
		((ScaleTransform)popup.RenderTransform).BeginAnimation(ScaleTransform.ScaleYProperty, animation);
	}

	private void CloseDownloadSourcePopup(Border overlay, Border popup)
	{
		Border overlay2 = overlay;
		Border popup2 = popup;
		CubicEase easingFunction = new CubicEase
		{
			EasingMode = EasingMode.EaseIn
		};
		TimeSpan timeSpan = TimeSpan.FromMilliseconds(150L, 0L);
		DoubleAnimation doubleAnimation = new DoubleAnimation(0.0, timeSpan)
		{
			EasingFunction = easingFunction
		};
		doubleAnimation.Completed += delegate
		{
			if (_versionSelectPanel?.Child is Grid grid)
			{
				grid.Children.Remove(overlay2);
				grid.Children.Remove(popup2);
			}
		};
		overlay2.BeginAnimation(UIElement.OpacityProperty, doubleAnimation);
		popup2.BeginAnimation(UIElement.OpacityProperty, doubleAnimation);
	}

	private void StartVersionDownload(ModrinthProject proj, ModrinthVersion ver, ResourceType resType, string mirror)
	{
		ModrinthVersion ver2 = ver;
		string mirror2 = mirror;
		if (1 == 0)
		{
		}
		string text = resType switch
		{
			ResourceType.Mod => "Mod", 
			ResourceType.Shader => "Shader", 
			ResourceType.ResourcePack => "ResourcePack", 
			_ => "Mod", 
		};
		if (1 == 0)
		{
		}
		string type = text;
		CloseVersionSelectPage();
		DownloadManager.StartResourceDownload(proj.Title + " " + ver2.VersionNumber, type, () => ModrinthApi.DownloadVersionAsync(ver2, resType, mirror2));
		ShowDownloadCenterPage();
	}

	private void StartDownload_Click(object sender, RoutedEventArgs e)
	{
		if (!string.IsNullOrEmpty(_selectedVersionId))
		{
			string loaderName = _selectedLoaderName ?? "";
			string loaderVersion = _selectedLoaderVersion ?? "";
			DownloadManager.StartDownload(_selectedVersionId, loaderName, loaderVersion);
			ShowDownloadCenterPage();
		}
	}

	private void ShowLaunchPage()
	{
		SwitchToPage(LaunchPage, LaunchPageSlide, delegate
		{
			ApplyConfigToLaunchPage();
			LoadInstalledVersions();
		});
	}

	private async void LoadInstalledVersions()
	{
		LaunchVersionList.Children.Clear();
		TextBlock loading = new TextBlock
		{
			Foreground = new SolidColorBrush(Color.FromRgb(136, 136, 136)),
			FontSize = 13.0,
			TextAlignment = TextAlignment.Center,
			Margin = new Thickness(0.0, 40.0, 0.0, 0.0)
		};
		loading.SetResourceReference(TextBlock.TextProperty, "StatusLoading");
		LaunchVersionList.Children.Add(loading);
		List<InstalledVersion> versions = await Task.Run(() => LaunchManager.GetInstalledVersions());
		LaunchVersionList.Children.Clear();
		if (versions.Count == 0)
		{
			TextBlock empty = new TextBlock
			{
				Foreground = new SolidColorBrush(Color.FromRgb(136, 136, 136)),
				FontSize = 13.0,
				TextAlignment = TextAlignment.Center,
				Margin = new Thickness(0.0, 40.0, 0.0, 0.0)
			};
			empty.SetResourceReference(TextBlock.TextProperty, "StatusNoInstalledVersion");
			LaunchVersionList.Children.Add(empty);
			return;
		}
		foreach (InstalledVersion ver in versions)
		{
			Border item = new Border
			{
				Height = 56.0,
				Margin = new Thickness(0.0, 2.0, 0.0, 0.0),
				Padding = new Thickness(14.0, 0.0, 14.0, 0.0),
				Background = new SolidColorBrush(Color.FromRgb(34, 34, 38)),
				CornerRadius = new CornerRadius(6.0),
				Cursor = Cursors.Hand,
				Tag = ver.Id
			};
			Grid grid = new Grid
			{
				ColumnDefinitions = 
				{
					new ColumnDefinition
					{
						Width = new GridLength(1.0, GridUnitType.Star)
					},
					new ColumnDefinition
					{
						Width = GridLength.Auto
					}
				}
			};
			StackPanel infoStack = new StackPanel
			{
				VerticalAlignment = VerticalAlignment.Center
			};
			TextBlock nameText = new TextBlock
			{
				Text = VersionSettingsManager.GetDisplayName(ver.Id),
				Foreground = Brushes.White,
				FontSize = 13.0,
				FontWeight = FontWeights.Medium,
				TextTrimming = TextTrimming.CharacterEllipsis
			};
			infoStack.Children.Add(nameText);
			TextBlock tagText = new TextBlock
			{
				FontSize = 11.0,
				Margin = new Thickness(0.0, 2.0, 0.0, 0.0),
				Foreground = new SolidColorBrush(Color.FromRgb(130, 130, 130))
			};
			List<string> tags = new List<string>();
			if (ver.IsForge)
			{
				tags.Add("Forge");
			}
			if (ver.IsFabric)
			{
				tags.Add("Fabric");
			}
			if (ver.IsQuilt)
			{
				tags.Add("Quilt");
			}
			tags.Add(ver.Type);
			if (!ver.HasJar)
			{
				tags.Add(LanguageManager.Get("ResMissingJar"));
			}
			tagText.Text = string.Join(" · ", tags);
			infoStack.Children.Add(tagText);
			Grid.SetColumn(infoStack, 0);
			grid.Children.Add(infoStack);
			Ellipse statusDot = new Ellipse
			{
				Width = 8.0,
				Height = 8.0,
				Fill = new SolidColorBrush(ver.HasJar ? Color.FromRgb(80, 200, 120) : Color.FromRgb(200, 160, 60)),
				VerticalAlignment = VerticalAlignment.Center,
				Margin = new Thickness(12.0, 0.0, 4.0, 0.0)
			};
			Grid.SetColumn(statusDot, 1);
			grid.Children.Add(statusDot);
			item.Child = grid;
			item.ToolTip = LanguageManager.Get("VerSettingsTitle") + " · " + LanguageManager.Get("CommonDoubleClick");
			item.MouseEnter += delegate(object s, MouseEventArgs _)
			{
				if (s is Border border5)
				{
					border5.Background = new SolidColorBrush(Color.FromRgb(44, 44, 48));
				}
			};
			item.MouseLeave += delegate(object s, MouseEventArgs _)
			{
				if (s is Border { Tag: var tag3 } border4 && tag3?.ToString() != _selectedLaunchVersionId)
				{
					border4.Background = new SolidColorBrush(Color.FromRgb(34, 34, 38));
				}
			};
			item.MouseLeftButtonDown += delegate(object s, MouseButtonEventArgs e)
			{
				if (s is Border { Tag: string tag2 } && e.ClickCount >= 2)
				{
					e.Handled = true;
					bool isModded = ver.IsForge || ver.IsFabric || ver.IsQuilt;
					ShowVersionSettings(tag2, LaunchPage, LaunchPageSlide, isModded);
				}
			};
			item.MouseLeftButtonUp += delegate(object s, MouseButtonEventArgs _)
			{
				if (s is Border { Tag: string tag })
				{
					_selectedLaunchVersionId = tag;
					SelectedLaunchVersion.Text = VersionSettingsManager.GetDisplayName(tag);
					LaunchButton.IsEnabled = true;
					foreach (object child in LaunchVersionList.Children)
					{
						if (child is Border border2)
						{
							border2.Background = ((border2.Tag?.ToString() == tag) ? new SolidColorBrush(Color.FromRgb(48, 72, 120)) : new SolidColorBrush(Color.FromRgb(34, 34, 38)));
						}
					}
				}
			};
			LaunchVersionList.Children.Add(item);
		}
	}

	private void RefreshVersions_Click(object sender, RoutedEventArgs e)
	{
		LoadInstalledVersions();
	}

	private async void LaunchGame_Click(object sender, RoutedEventArgs e)
	{
		if (string.IsNullOrEmpty(_selectedLaunchVersionId))
		{
			NotificationManager.Show(LanguageManager.Get("MsgSelectVersion"));
			return;
		}
		string username = PlayerNameBox.Text.Trim();
		if (string.IsNullOrEmpty(username))
		{
			NotificationManager.Show(LanguageManager.Get("MsgEnterPlayerName"));
			return;
		}
		if (!int.TryParse(RamBox.Text.Trim(), out var globalRam) || globalRam < 512)
		{
			NotificationManager.Show(LanguageManager.Get("MsgInvalidRam"));
			return;
		}
		VersionSettings verSettings = VersionSettingsManager.Get(_selectedLaunchVersionId);
		int ram = (verSettings.UseCustomMemory ? verSettings.CustomMemoryMb : globalRam);
		if (ram < 512)
		{
			NotificationManager.Show(LanguageManager.Get("MsgInvalidRam"));
			return;
		}
		LauncherConfig.Current.PlayerName = username;
		LauncherConfig.Current.MaxRamMb = globalRam;
		LauncherConfig.Save();
		LaunchButton.IsEnabled = false;
		LaunchButton.SetResourceReference(ContentControl.ContentProperty, "LaunchLaunching");
		LaunchStatusText.Visibility = Visibility.Collapsed;
		await LaunchManager.LaunchAsync(_selectedLaunchVersionId, username, ram);
	}

	private void ApplyConfigToLaunchPage()
	{
		if (PlayerNameBox != null && RamBox != null)
		{
			PlayerNameBox.Text = LauncherConfig.Current.PlayerName;
			RamBox.Text = LauncherConfig.Current.MaxRamMb.ToString();
		}
	}

	private void ShowMorePage()
	{
		SwitchToPage(MorePage, MorePageSlide, delegate
		{
			try
			{
				if (MoreVersionText != null)
				{
					MoreVersionText.Text = UpdateChecker.CurrentVersion;
				}
				if (MoreRuntimeText != null)
				{
					string text = Environment.Version.ToString();
					MoreRuntimeText.Text = ".NET " + text;
				}
			}
			catch
			{
			}
		});
	}

	private void MoreOpenGameDir_Click(object sender, RoutedEventArgs e)
	{
		try
		{
			string minecraftPath = DownloadManager.MinecraftPath;
			if (!Directory.Exists(minecraftPath))
			{
				Directory.CreateDirectory(minecraftPath);
			}
			Process.Start(new ProcessStartInfo("explorer.exe", minecraftPath)
			{
				UseShellExecute = true
			});
		}
		catch (Exception ex)
		{
			NotificationManager.Show(string.Format(LanguageManager.Get("MsgOpenDirFailed"), ex.Message));
		}
	}

	private void MoreOpenJavaDir_Click(object sender, RoutedEventArgs e)
	{
		try
		{
			string text = System.IO.Path.Combine(DownloadManager.MinecraftPath, "runtime");
			if (Directory.Exists(text))
			{
				Process.Start(new ProcessStartInfo("explorer.exe", text)
				{
					UseShellExecute = true
				});
			}
			else
			{
				NotificationManager.Show(LanguageManager.Get("MsgNoRuntimeDir"));
			}
		}
		catch (Exception ex)
		{
			NotificationManager.Show(string.Format(LanguageManager.Get("MsgOpenDirFailed"), ex.Message));
		}
	}

	private void MoreClearCache_Click(object sender, RoutedEventArgs e)
	{
		try
		{
			string path = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "cache");
			if (Directory.Exists(path))
			{
				Directory.Delete(path, recursive: true);
				Directory.CreateDirectory(path);
			}
			NotificationManager.Show(LanguageManager.Get("MsgCacheCleared"));
		}
		catch (Exception ex)
		{
			NotificationManager.Show(string.Format(LanguageManager.Get("MsgClearCacheFailed"), ex.Message));
		}
	}

	private async void MoreCheckUpdate_Click(object sender, RoutedEventArgs e)
	{
		try
		{
			if (MoreCheckUpdateBtn != null)
			{
				MoreCheckUpdateBtn.SetResourceReference(ContentControl.ContentProperty, "MoreChecking");
				MoreCheckUpdateBtn.IsEnabled = false;
			}
			bool found = false;
			UpdateInfo foundInfo = null;
			string errorMsg = null;
			UpdateChecker.UpdateAvailable += OnFound;
			UpdateChecker.CheckFailed += OnFail;
			await UpdateChecker.CheckAsync(silent: false);
			UpdateChecker.UpdateAvailable -= OnFound;
			UpdateChecker.CheckFailed -= OnFail;
			if (found && foundInfo != null)
			{
				NotificationManager.Show(string.Format(arg0: foundInfo.TagName.TrimStart('v', 'V'), format: LanguageManager.Get("MsgUpdateFound")));
				if (!string.IsNullOrEmpty(foundInfo.HtmlUrl))
				{
					Process.Start(new ProcessStartInfo(foundInfo.HtmlUrl)
					{
						UseShellExecute = true
					});
				}
			}
			else
			{
				NotificationManager.Show(string.Format(LanguageManager.Get("MsgAlreadyLatest"), UpdateChecker.CurrentVersion));
			}
			void OnFail(string msg)
			{
				errorMsg = msg;
			}
			void OnFound(UpdateInfo info)
			{
				found = true;
				foundInfo = info;
			}
		}
		catch (Exception ex2)
		{
			Exception ex = ex2;
			NotificationManager.Show(string.Format(LanguageManager.Get("MsgCheckUpdateFailed"), ex.Message));
		}
		finally
		{
			if (MoreCheckUpdateBtn != null)
			{
				MoreCheckUpdateBtn.SetResourceReference(ContentControl.ContentProperty, "MoreCheckUpdate");
				MoreCheckUpdateBtn.IsEnabled = true;
			}
		}
	}

	public void PlayEnterAnimation()
	{
		base.Visibility = Visibility.Visible;
		base.Opacity = 1.0;
		_panelSlide.BeginAnimation(TranslateTransform.XProperty, null);
		_panelSlide.X = 200.0;
		CubicEase easingFunction = new CubicEase
		{
			EasingMode = EasingMode.EaseOut
		};
		DoubleAnimation doubleAnimation = new DoubleAnimation(0.0, TimeSpan.FromMilliseconds(350L, 0L))
		{
			EasingFunction = easingFunction,
			BeginTime = TimeSpan.FromMilliseconds(20L, 0L)
		};
		doubleAnimation.Completed += delegate
		{
			Focus();
			if (!IsAnyPageVisible())
			{
				ShowLaunchPage();
			}
			base.Dispatcher.BeginInvoke((Action)delegate
			{
				Focus();
				if (_currentResourceType != "游戏" && ResourceSearchBox != null)
				{
					ResourceSearchBox.Focus();
					base.Dispatcher.BeginInvoke((Action)delegate
					{
						ResourceSearchBox.Focus();
						Keyboard.Focus(ResourceSearchBox);
					}, DispatcherPriority.Input);
				}
			}, DispatcherPriority.ContextIdle);
		};
		_panelSlide.BeginAnimation(TranslateTransform.XProperty, doubleAnimation);
		AnimateNavItems(isEnter: true);
	}

	public void PlayExitAnimation()
	{
		_panelSlide.BeginAnimation(TranslateTransform.XProperty, null);
		CubicEase easingFunction = new CubicEase
		{
			EasingMode = EasingMode.EaseIn
		};
		DoubleAnimation doubleAnimation = new DoubleAnimation(-200.0, TimeSpan.FromMilliseconds(220L, 0L))
		{
			EasingFunction = easingFunction
		};
		doubleAnimation.Completed += delegate
		{
			base.Visibility = Visibility.Collapsed;
			base.Opacity = 0.0;
		};
		_panelSlide.BeginAnimation(TranslateTransform.XProperty, doubleAnimation);
	}

	private void CollapseNav_Click(object sender, MouseButtonEventArgs e)
	{
		this.CollapseRequested?.Invoke(this, EventArgs.Empty);
	}

	private void ExitNav_Click(object sender, MouseButtonEventArgs e)
	{
		this.ExitRequested?.Invoke(this, EventArgs.Empty);
	}

	private void CloseBtn_Click(object sender, MouseButtonEventArgs e)
	{
		this.CollapseRequested?.Invoke(this, EventArgs.Empty);
	}

	private void CloseBtn_MouseEnter(object sender, MouseEventArgs e)
	{
		if (sender is Border border)
		{
			ColorAnimation animation = new ColorAnimation(Color.FromRgb(70, 70, 74), TimeSpan.FromMilliseconds(150L, 0L));
			border.Background.BeginAnimation(SolidColorBrush.ColorProperty, animation);
		}
	}

	private void CloseBtn_MouseLeave(object sender, MouseEventArgs e)
	{
		if (sender is Border border)
		{
			ColorAnimation animation = new ColorAnimation(Color.FromRgb(45, 45, 48), TimeSpan.FromMilliseconds(150L, 0L));
			border.Background.BeginAnimation(SolidColorBrush.ColorProperty, animation);
		}
	}

	private void AnimateNavItems(bool isEnter)
	{
		if (!base.IsLoaded)
		{
			return;
		}
		int num = 60;
		CubicEase easingFunction = new CubicEase
		{
			EasingMode = EasingMode.EaseOut
		};
		List<FrameworkElement> list = new List<FrameworkElement> { CollapseNav };
		foreach (object child in LeftNav.Children)
		{
			list.Add((FrameworkElement)child);
		}
		list.Add(ExitNav);
		foreach (FrameworkElement item in list)
		{
			Border border = item as Border;
			if (border == null || !(border.RenderTransform is TransformGroup transformGroup))
			{
				continue;
			}
			Transform transform2 = transformGroup.Children[0];
			TranslateTransform transform = transform2 as TranslateTransform;
			if (transform == null)
			{
				continue;
			}
			if (isEnter)
			{
				border.BeginAnimation(UIElement.OpacityProperty, null);
				transform.BeginAnimation(TranslateTransform.XProperty, null);
				transform.X = -60.0;
				border.Opacity = 0.0;
				DoubleAnimation doubleAnimation = new DoubleAnimation(-60.0, 0.0, TimeSpan.FromMilliseconds(280L, 0L))
				{
					EasingFunction = easingFunction,
					BeginTime = TimeSpan.FromMilliseconds(num, 0L)
				};
				doubleAnimation.Completed += delegate
				{
					transform.BeginAnimation(TranslateTransform.XProperty, null);
					transform.X = 0.0;
				};
				transform.BeginAnimation(TranslateTransform.XProperty, doubleAnimation);
				DoubleAnimation doubleAnimation2 = new DoubleAnimation(0.0, 1.0, TimeSpan.FromMilliseconds(220L, 0L))
				{
					BeginTime = TimeSpan.FromMilliseconds(num, 0L)
				};
				doubleAnimation2.Completed += delegate
				{
					border.BeginAnimation(UIElement.OpacityProperty, null);
					border.Opacity = 1.0;
				};
				border.BeginAnimation(UIElement.OpacityProperty, doubleAnimation2);
			}
			else
			{
				border.BeginAnimation(UIElement.OpacityProperty, null);
				transform.BeginAnimation(TranslateTransform.XProperty, null);
				DoubleAnimation doubleAnimation3 = new DoubleAnimation(0.0, -60.0, TimeSpan.FromMilliseconds(180L, 0L))
				{
					EasingFunction = easingFunction
				};
				doubleAnimation3.Completed += delegate
				{
					transform.BeginAnimation(TranslateTransform.XProperty, null);
					transform.X = 0.0;
				};
				transform.BeginAnimation(TranslateTransform.XProperty, doubleAnimation3);
				DoubleAnimation doubleAnimation4 = new DoubleAnimation(1.0, 0.0, TimeSpan.FromMilliseconds(150L, 0L));
				doubleAnimation4.Completed += delegate
				{
					border.BeginAnimation(UIElement.OpacityProperty, null);
					border.Opacity = 0.0;
				};
				border.BeginAnimation(UIElement.OpacityProperty, doubleAnimation4);
			}
			num += 50;
		}
	}

	private void NavItem_MouseEnter(object sender, MouseEventArgs e)
	{
		if (sender is Border { RenderTransform: TransformGroup renderTransform } && renderTransform.Children[0] is TranslateTransform translateTransform)
		{
			DoubleAnimation animation = new DoubleAnimation(8.0, TimeSpan.FromMilliseconds(250L, 0L))
			{
				EasingFunction = new CubicEase
				{
					EasingMode = EasingMode.EaseOut
				}
			};
			translateTransform.BeginAnimation(TranslateTransform.XProperty, animation);
		}
	}

	private void NavItem_MouseLeave(object sender, MouseEventArgs e)
	{
		if (sender is Border { RenderTransform: TransformGroup renderTransform } border && renderTransform.Children[0] is TranslateTransform translateTransform)
		{
			int num = ((border == _selectedItem) ? 8 : 0);
			DoubleAnimation animation = new DoubleAnimation(num, TimeSpan.FromMilliseconds(200L, 0L))
			{
				EasingFunction = new CubicEase
				{
					EasingMode = EasingMode.EaseIn
				}
			};
			translateTransform.BeginAnimation(TranslateTransform.XProperty, animation);
		}
	}

	private void Nav_Click(object sender, MouseButtonEventArgs e)
	{
		if (!(sender is Border { Tag: not null, Tag: var tag } border))
		{
			return;
		}
		string text = tag?.ToString() ?? "";
		foreach (object child in LeftNav.Children)
		{
			if (child is Border border2)
			{
				TextBlock textBlock = FindTextBlock(border2);
				if (textBlock != null)
				{
					textBlock.Foreground = ((border2 == border) ? new SolidColorBrush(_currentThemeColor) : new SolidColorBrush(Color.FromRgb(byte.MaxValue, byte.MaxValue, byte.MaxValue)));
				}
				if (border2.RenderTransform is TransformGroup transformGroup && transformGroup.Children[0] is TranslateTransform translateTransform && transformGroup.Children[1] is ScaleTransform scaleTransform)
				{
					bool flag = border2 == border;
					int num = (flag ? 8 : 0);
					double toValue = (flag ? 1.08 : 1.0);
					CubicEase easingFunction = new CubicEase
					{
						EasingMode = EasingMode.EaseOut
					};
					TimeSpan timeSpan = AnimDuration(200.0);
					DoubleAnimation animation = new DoubleAnimation(num, timeSpan)
					{
						EasingFunction = easingFunction
					};
					translateTransform.BeginAnimation(TranslateTransform.XProperty, animation);
					DoubleAnimation animation2 = new DoubleAnimation(toValue, timeSpan)
					{
						EasingFunction = easingFunction
					};
					scaleTransform.BeginAnimation(ScaleTransform.ScaleXProperty, animation2);
					scaleTransform.BeginAnimation(ScaleTransform.ScaleYProperty, animation2);
				}
			}
		}
		_selectedItem = border;
		switch (text)
		{
		case "launch":
			ShowLaunchPage();
			break;
		case "download":
			ShowDownloadPage();
			break;
		case "settings":
			ShowSettingsPage();
			break;
		case "more":
			ShowMorePage();
			break;
		case "downloadcenter":
			ShowDownloadCenterPage();
			break;
		}
	}

	private TextBlock? FindTextBlock(DependencyObject parent)
	{
		for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
		{
			DependencyObject child = VisualTreeHelper.GetChild(parent, i);
			if (child is TextBlock result)
			{
				return result;
			}
			TextBlock textBlock = FindTextBlock(child);
			if (textBlock != null)
			{
				return textBlock;
			}
		}
		return null;
	}

	private bool IsAnyPageVisible()
	{
		return LaunchPage.Visibility == Visibility.Visible || DownloadPage.Visibility == Visibility.Visible || DownloadCenterPage.Visibility == Visibility.Visible || VersionSettingsPage.Visibility == Visibility.Visible || SettingsPage.Visibility == Visibility.Visible || MorePage.Visibility == Visibility.Visible;
	}

	private Grid? GetVisiblePage()
	{
		if (LaunchPage.Visibility == Visibility.Visible)
		{
			return LaunchPage;
		}
		if (DownloadPage.Visibility == Visibility.Visible)
		{
			return DownloadPage;
		}
		if (DownloadCenterPage.Visibility == Visibility.Visible)
		{
			return DownloadCenterPage;
		}
		if (VersionSettingsPage.Visibility == Visibility.Visible)
		{
			return VersionSettingsPage;
		}
		if (SettingsPage.Visibility == Visibility.Visible)
		{
			return SettingsPage;
		}
		if (MorePage.Visibility == Visibility.Visible)
		{
			return MorePage;
		}
		return null;
	}

	private void AnimatePageOut(Grid page, Action? onCompleted = null)
	{
		Grid page2 = page;
		Action onCompleted2 = onCompleted;
		if (!(page2.RenderTransform is TranslateTransform translateTransform))
		{
			page2.Visibility = Visibility.Collapsed;
			onCompleted2?.Invoke();
			return;
		}
		translateTransform.BeginAnimation(TranslateTransform.XProperty, null);
		page2.BeginAnimation(UIElement.OpacityProperty, null);
		CubicEase easingFunction = new CubicEase
		{
			EasingMode = EasingMode.EaseIn
		};
		TimeSpan timeSpan = AnimDuration(260.0);
		DoubleAnimation doubleAnimation = new DoubleAnimation(-60.0, timeSpan)
		{
			EasingFunction = easingFunction
		};
		DoubleAnimation animation = new DoubleAnimation(0.0, timeSpan)
		{
			EasingFunction = easingFunction
		};
		doubleAnimation.Completed += delegate
		{
			page2.Visibility = Visibility.Collapsed;
			onCompleted2?.Invoke();
		};
		translateTransform.BeginAnimation(TranslateTransform.XProperty, doubleAnimation);
		page2.BeginAnimation(UIElement.OpacityProperty, animation);
	}

	private void AnimatePageIn(Grid page, TranslateTransform slide, Action? afterShow = null)
	{
		ContentArea.Visibility = Visibility.Visible;
		slide.BeginAnimation(TranslateTransform.XProperty, null);
		page.BeginAnimation(UIElement.OpacityProperty, null);
		slide.X = 60.0;
		page.Opacity = 0.0;
		page.Visibility = Visibility.Visible;
		CubicEase easingFunction = new CubicEase
		{
			EasingMode = EasingMode.EaseOut
		};
		TimeSpan timeSpan = AnimDuration(320.0);
		DoubleAnimation animation = new DoubleAnimation(0.0, timeSpan)
		{
			EasingFunction = easingFunction
		};
		DoubleAnimation animation2 = new DoubleAnimation(1.0, timeSpan)
		{
			EasingFunction = easingFunction
		};
		slide.BeginAnimation(TranslateTransform.XProperty, animation);
		page.BeginAnimation(UIElement.OpacityProperty, animation2);
		afterShow?.Invoke();
	}

	private void SwitchToPage(Grid newPage, TranslateTransform newSlide, Action? afterShow = null)
	{
		Grid visiblePage = GetVisiblePage();
		if (visiblePage == null || visiblePage == newPage)
		{
			AnimatePageIn(newPage, newSlide, afterShow);
			return;
		}
		AnimatePageOut(visiblePage);
		AnimatePageIn(newPage, newSlide, afterShow);
	}

	private void HideContentArea()
	{
		ContentArea.Visibility = Visibility.Collapsed;
		DownloadPage.Visibility = Visibility.Collapsed;
		DownloadCenterPage.Visibility = Visibility.Collapsed;
		LaunchPage.Visibility = Visibility.Collapsed;
		VersionSettingsPage.Visibility = Visibility.Collapsed;
		SettingsPage.Visibility = Visibility.Collapsed;
	}

	private void ApplyPersonalization()
	{
		try
		{
			Color themeColor = LauncherConfig.GetThemeColor();
			Color color = LauncherConfig.ApplyHslAdjust(themeColor);
			SolidColorBrush solidColorBrush = new SolidColorBrush(color);
			solidColorBrush.Freeze();
			_currentThemeColor = color;
			if (base.Resources != null)
			{
				base.Resources["ColorBrush3"] = solidColorBrush;
			}
			if (_selectedSettingsTab != null)
			{
				_selectedSettingsTab.Background = solidColorBrush;
			}
			foreach (object child in SettingsTabPanel.Children)
			{
				if (child is Border border && border == _selectedSettingsTab)
				{
					border.Background = solidColorBrush;
				}
			}
			if (_selectedItem != null)
			{
				TextBlock textBlock = FindTextBlock(_selectedItem);
				if (textBlock != null)
				{
					textBlock.Foreground = solidColorBrush;
				}
			}
			double num = (double)LauncherConfig.Current.Opacity / 100.0;
			if (ContentArea != null)
			{
				ContentArea.Opacity = 0.3 + num * 0.7;
			}
		}
		catch
		{
		}
	}

	private void ShowVersionSettings(string versionId, Grid returnPage, TranslateTransform returnSlide, bool isModded = false)
	{
		_verSettingsVersionId = versionId;
		_returnPageAfterVerSettings = returnPage.Name;
		string displayName = VersionSettingsManager.GetDisplayName(versionId);
		VerSettingsTitleText.Text = displayName;
		VerSettingsNameBox.Text = displayName;
		VersionSettings versionSettings = VersionSettingsManager.Get(versionId);
		if (versionSettings.UseCustomMemory)
		{
			VerSettingsMemCustom.IsChecked = true;
			VerSettingsMemBox.IsEnabled = true;
			VerSettingsMemBox.Text = versionSettings.CustomMemoryMb.ToString();
		}
		else
		{
			VerSettingsMemGlobal.IsChecked = true;
			VerSettingsMemBox.IsEnabled = false;
			VerSettingsMemBox.Text = LauncherConfig.Current.MaxRamMb.ToString();
		}
		VerSettingsJvmBox.Text = versionSettings.JvmArgs ?? "";
		VerSettingsGameArgsBox.Text = versionSettings.GameArgs ?? "";
		if (isModded)
		{
			VerSettingsModsArea.Visibility = Visibility.Visible;
			RefreshModsList();
		}
		else
		{
			VerSettingsModsArea.Visibility = Visibility.Collapsed;
		}
		SwitchToPage(VersionSettingsPage, VersionSettingsPageSlide);
	}

	private void RefreshModsList()
	{
		VerSettingsModsListPanel.Children.Clear();
		if (string.IsNullOrEmpty(_verSettingsVersionId))
		{
			return;
		}
		LaunchManager.EnsureVersionIsolationDirs(_verSettingsVersionId);
		string path = System.IO.Path.Combine(LaunchManager.GetVersionGameDir(_verSettingsVersionId), "mods");
		List<FileInfo> list = new List<FileInfo>();
		if (Directory.Exists(path))
		{
			list = (from f in Directory.GetFiles(path, "*.jar")
				select new FileInfo(f) into f
				orderby f.Name
				select f).ToList();
		}
		if (list.Count == 0)
		{
			TextBlock textBlock = new TextBlock();
			textBlock.SetResourceReference(TextBlock.TextProperty, "VerSettingsModsEmpty");
			textBlock.Foreground = new SolidColorBrush(Color.FromRgb(110, 110, 110));
			textBlock.FontSize = 12.0;
			textBlock.HorizontalAlignment = HorizontalAlignment.Center;
			VerSettingsModsListPanel.Children.Add(textBlock);
			return;
		}
		TextBlock textBlock2 = new TextBlock
		{
			Foreground = new SolidColorBrush(Color.FromRgb(160, 160, 160)),
			FontSize = 12.0,
			HorizontalAlignment = HorizontalAlignment.Center,
			Margin = new Thickness(0.0, 0.0, 0.0, 8.0)
		};
		textBlock2.SetResourceReference(TextBlock.TextProperty, "VerSettingsModsCount");
		textBlock2.Text = $"{list.Count} {textBlock2.Text}";
		VerSettingsModsListPanel.Children.Add(textBlock2);
		foreach (FileInfo item in list)
		{
			Border border = new Border
			{
				Height = 30.0,
				Margin = new Thickness(0.0, 2.0, 0.0, 0.0),
				Padding = new Thickness(10.0, 0.0, 10.0, 0.0),
				Background = new SolidColorBrush(Color.FromRgb(38, 38, 42)),
				CornerRadius = new CornerRadius(4.0),
				Cursor = Cursors.Hand,
				Tag = item.FullName
			};
			Grid grid = new Grid();
			grid.ColumnDefinitions.Add(new ColumnDefinition
			{
				Width = new GridLength(1.0, GridUnitType.Star)
			});
			grid.ColumnDefinitions.Add(new ColumnDefinition
			{
				Width = GridLength.Auto
			});
			TextBlock element = new TextBlock
			{
				Text = item.Name,
				Foreground = Brushes.White,
				FontSize = 12.0,
				VerticalAlignment = VerticalAlignment.Center,
				TextTrimming = TextTrimming.CharacterEllipsis
			};
			Grid.SetColumn(element, 0);
			grid.Children.Add(element);
			TextBlock textBlock3 = new TextBlock
			{
				Text = "✕",
				Foreground = new SolidColorBrush(Color.FromRgb(200, 80, 80)),
				FontSize = 12.0,
				VerticalAlignment = VerticalAlignment.Center,
				Margin = new Thickness(8.0, 0.0, 0.0, 0.0),
				Cursor = Cursors.Hand,
				Tag = item.FullName
			};
			textBlock3.MouseLeftButtonUp += ModDelete_Click;
			Grid.SetColumn(textBlock3, 1);
			grid.Children.Add(textBlock3);
			border.Child = grid;
			VerSettingsModsListPanel.Children.Add(border);
		}
	}

	private void ModDelete_Click(object sender, MouseButtonEventArgs e)
	{
		if (!(sender is TextBlock { Tag: string tag }))
		{
			return;
		}
		try
		{
			if (File.Exists(tag))
			{
				File.Delete(tag);
			}
			RefreshModsList();
			NotificationManager.Show(LanguageManager.Get("VerSettingsModRemoved"));
		}
		catch (Exception)
		{
		}
	}

	private void VerSettingsBack_Click(object sender, RoutedEventArgs e)
	{
		ReturnToLaunchPage();
	}

	private void ReturnToLaunchPage()
	{
		if (_returnPageAfterVerSettings == "LaunchPage")
		{
			SwitchToPage(LaunchPage, LaunchPageSlide, delegate
			{
				LoadInstalledVersions();
			});
		}
		else
		{
			SwitchToPage(LaunchPage, LaunchPageSlide);
		}
	}

	private void VerSettingsMem_Checked(object sender, RoutedEventArgs e)
	{
		bool valueOrDefault = VerSettingsMemCustom.IsChecked.GetValueOrDefault();
		VerSettingsMemBox.IsEnabled = valueOrDefault;
		if (!valueOrDefault)
		{
			VerSettingsMemBox.Text = LauncherConfig.Current.MaxRamMb.ToString();
		}
	}

	private void VerSettingsOpenFolder_Click(object sender, RoutedEventArgs e)
	{
		if (string.IsNullOrEmpty(_verSettingsVersionId))
		{
			return;
		}
		try
		{
			LaunchManager.EnsureVersionIsolationDirs(_verSettingsVersionId);
			string versionGameDir = LaunchManager.GetVersionGameDir(_verSettingsVersionId);
			if (!Directory.Exists(versionGameDir))
			{
				Directory.CreateDirectory(versionGameDir);
			}
			Process.Start(new ProcessStartInfo
			{
				FileName = versionGameDir,
				UseShellExecute = true,
				Verb = "open"
			});
		}
		catch (Exception)
		{
			NotificationManager.Show(LanguageManager.Get("VerSettingsOpenFolderFail"));
		}
	}

	private void VerSettingsMods_DragEnter(object sender, DragEventArgs e)
	{
		if (HasJarFiles(e.Data))
		{
			VerSettingsModsDropArea.Background = new SolidColorBrush(Color.FromRgb(45, 60, 90));
			VerSettingsModsDropArea.BorderBrush = new SolidColorBrush(Color.FromRgb(72, 144, 245));
			VerSettingsModsDropText.Visibility = Visibility.Visible;
			VerSettingsModsListPanel.Visibility = Visibility.Collapsed;
		}
		e.Effects = (HasJarFiles(e.Data) ? DragDropEffects.Copy : DragDropEffects.None);
		e.Handled = true;
	}

	private void VerSettingsMods_DragLeave(object sender, DragEventArgs e)
	{
		VerSettingsModsDropArea.Background = new SolidColorBrush(Color.FromRgb(45, 45, 48));
		VerSettingsModsDropArea.BorderBrush = new SolidColorBrush(Color.FromRgb(61, 61, 66));
		VerSettingsModsDropText.Visibility = Visibility.Collapsed;
		VerSettingsModsListPanel.Visibility = Visibility.Visible;
		e.Handled = true;
	}

	private void VerSettingsMods_Drop(object sender, DragEventArgs e)
	{
		VerSettingsModsDropArea.Background = new SolidColorBrush(Color.FromRgb(45, 45, 48));
		VerSettingsModsDropArea.BorderBrush = new SolidColorBrush(Color.FromRgb(61, 61, 66));
		VerSettingsModsDropText.Visibility = Visibility.Collapsed;
		VerSettingsModsListPanel.Visibility = Visibility.Visible;
		if (string.IsNullOrEmpty(_verSettingsVersionId) || !HasJarFiles(e.Data))
		{
			return;
		}
		string[] array = (string[])e.Data.GetData(DataFormats.FileDrop);
		if (array == null)
		{
			return;
		}
		LaunchManager.EnsureVersionIsolationDirs(_verSettingsVersionId);
		string text = System.IO.Path.Combine(LaunchManager.GetVersionGameDir(_verSettingsVersionId), "mods");
		Directory.CreateDirectory(text);
		int num = 0;
		string[] array2 = array;
		foreach (string text2 in array2)
		{
			if (text2.EndsWith(".jar", StringComparison.OrdinalIgnoreCase))
			{
				try
				{
					string destFileName = System.IO.Path.Combine(text, System.IO.Path.GetFileName(text2));
					File.Copy(text2, destFileName, overwrite: true);
					num++;
				}
				catch (Exception)
				{
				}
			}
		}
		if (num > 0)
		{
			RefreshModsList();
			NotificationManager.Show(string.Format(LanguageManager.Get("VerSettingsModInstalled"), System.IO.Path.GetFileName(array[0])));
		}
		e.Handled = true;
	}

	private static bool HasJarFiles(IDataObject data)
	{
		if (!data.GetDataPresent(DataFormats.FileDrop))
		{
			return false;
		}
		return ((string[])data.GetData(DataFormats.FileDrop))?.Any((string f) => f.EndsWith(".jar", StringComparison.OrdinalIgnoreCase)) ?? false;
	}

	private void VerSettingsDelete_Click(object sender, RoutedEventArgs e)
	{
		if (string.IsNullOrEmpty(_verSettingsVersionId))
		{
			return;
		}
		string versionDir = System.IO.Path.Combine(DownloadManager.MinecraftPath, "versions", _verSettingsVersionId);
		if (!System.IO.Directory.Exists(versionDir))
		{
			return;
		}
		try
		{
			System.IO.Directory.Delete(versionDir, recursive: true);
			VersionSettingsManager.Remove(_verSettingsVersionId);
			DataCache.Clear();
			NotificationManager.Show(LanguageManager.Get("VerSettingsDeleted"));
			ReturnToLaunchPage();
			RefreshVersionList();
		}
		catch (Exception ex)
		{
			NotificationManager.Show(string.Format(LanguageManager.Get("MsgOpenConfigFailed"), ex.Message));
		}
	}

	private void VerSettingsSave_Click(object sender, RoutedEventArgs e)
	{
		if (string.IsNullOrEmpty(_verSettingsVersionId))
		{
			return;
		}
		VersionSettings versionSettings = new VersionSettings
		{
			VersionId = _verSettingsVersionId,
			DisplayName = VerSettingsNameBox.Text.Trim(),
			UseCustomMemory = VerSettingsMemCustom.IsChecked.GetValueOrDefault(),
			JvmArgs = VerSettingsJvmBox.Text.Trim(),
			GameArgs = VerSettingsGameArgsBox.Text.Trim()
		};
		if (versionSettings.UseCustomMemory)
		{
			if (!int.TryParse(VerSettingsMemBox.Text.Trim(), out var result) || result < 512)
			{
				NotificationManager.Show(LanguageManager.Get("MsgInvalidRam"));
				return;
			}
			versionSettings.CustomMemoryMb = result;
		}
		VersionSettingsManager.Set(versionSettings);
		NotificationManager.Show(LanguageManager.Get("VerSettingsSaved"));
		if (sender is Button button)
		{
			ScaleTransform scaleTransform = new ScaleTransform(0.92, 0.92);
			button.RenderTransformOrigin = new Point(0.5, 0.5);
			button.RenderTransform = scaleTransform;
			DoubleAnimation animation = new DoubleAnimation(1.0, TimeSpan.FromMilliseconds(180L, 0L))
			{
				EasingFunction = new BackEase
				{
					Amplitude = 0.3,
					EasingMode = EasingMode.EaseOut
				}
			};
			scaleTransform.BeginAnimation(ScaleTransform.ScaleXProperty, animation);
			scaleTransform.BeginAnimation(ScaleTransform.ScaleYProperty, animation);
		}
	}

	private void CfgRamType_Changed(object sender, RoutedEventArgs e)
	{
		if (!_suppressConfigSave && _settingsInitialized)
		{
			bool valueOrDefault = CfgRamCustom.IsChecked.GetValueOrDefault();
			CfgRamSlider.IsEnabled = valueOrDefault;
			LauncherConfig.Current.RamType = (valueOrDefault ? 1 : 0);
			if (!valueOrDefault)
			{
				LauncherConfig.Current.MaxRamMb = (int)CfgRamSlider.Value;
			}
			LauncherConfig.Save();
			ApplyConfigToLaunchPage();
		}
	}

	private void CfgRamSlider_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
	{
		if (!_suppressConfigSave && _settingsInitialized)
		{
			int num = (int)CfgRamSlider.Value;
			CfgRamLabel.Text = $"{num} MB";
			if (CfgRamCustom.IsChecked.GetValueOrDefault())
			{
				LauncherConfig.Current.MaxRamMb = num;
				LauncherConfig.Save();
				ApplyConfigToLaunchPage();
			}
		}
	}

	private void CfgPersonalization_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
	{
		if (!_suppressConfigSave && _settingsInitialized)
		{
			LauncherConfig.Current.Opacity = (int)CfgOpacity.Value;
			LauncherConfig.Current.Hue = (int)CfgHue.Value;
			LauncherConfig.Current.Saturation = (int)CfgSaturation.Value;
			LauncherConfig.Current.Lightness = (int)CfgLightness.Value;
			LauncherConfig.Current.HueDelta = (int)CfgHueDelta.Value;
			LauncherConfig.Current.AnimationSpeed = (int)CfgAnimationSpeed.Value;
			if (CfgAnimationSpeedLabel != null)
			{
				CfgAnimationSpeedLabel.Text = $"{(int)CfgAnimationSpeed.Value}%";
			}
			LauncherConfig.Save();
			ApplyPersonalization();
		}
	}

	private void CfgLanguage_SelectionChanged(object sender, SelectionChangedEventArgs e)
	{
		if (!_suppressConfigSave && _settingsInitialized && !_languageApplying && CfgLanguage.SelectedIndex >= 0)
		{
			string text = ((CfgLanguage.SelectedIndex == 1) ? "zh_CN" : "en_US");
			LauncherConfig.Current.Language = text;
			LauncherConfig.Save();
			LanguageManager.Apply(text);
		}
	}

	private void CfgOpenBackgroundFolder_Click(object sender, RoutedEventArgs e)
	{
		string path = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Backgrounds");
		Directory.CreateDirectory(path);
		OpenFolderInExplorer(path);
	}

	private void CfgRefreshBackground_Click(object sender, RoutedEventArgs e)
	{
		NotificationManager.Show(LanguageManager.Get("MsgBackgroundRefreshed"));
	}

	private void CfgOpenMusicFolder_Click(object sender, RoutedEventArgs e)
	{
		string path = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Music");
		Directory.CreateDirectory(path);
		OpenFolderInExplorer(path);
	}

	private void CfgRefreshMusic_Click(object sender, RoutedEventArgs e)
	{
		NotificationManager.Show(LanguageManager.Get("MsgMusicRefreshed"));
	}

	private void CfgOpenConfigFile_Click(object sender, RoutedEventArgs e)
	{
		try
		{
			if (!File.Exists(LauncherConfig.ConfigFilePath))
			{
				LauncherConfig.Save();
			}
			OpenFolderInExplorer(LauncherConfig.ConfigFilePath);
		}
		catch (Exception ex)
		{
			NotificationManager.Show(string.Format(LanguageManager.Get("MsgOpenConfigFailed"), ex.Message));
		}
	}

	private void CfgResetAll_Click(object sender, RoutedEventArgs e)
	{
		LauncherConfig.Reset();
		InitSettingsControls();
		ApplyPersonalization();
		ApplyConfigToLaunchPage();
		NotificationManager.Show(LanguageManager.Get("MsgSettingsReset"));
	}

	private static void OpenFolderInExplorer(string path)
	{
		try
		{
			Process.Start(new ProcessStartInfo
			{
				FileName = "explorer.exe",
				Arguments = "\"" + path + "\"",
				UseShellExecute = true
			});
		}
		catch
		{
		}
	}

	private void InitSettingsControls()
	{
		_suppressConfigSave = true;
		LauncherConfigData current = LauncherConfig.Current;
		CfgVersionIsolation.SelectedIndex = current.VersionIsolation;
		CfgWindowTitle.Text = current.WindowTitle;
		CfgCustomInfo.Text = current.CustomInfo;
		CfgLauncherVisibility.SelectedIndex = current.LauncherVisibility;
		CfgProcessPriority.SelectedIndex = current.ProcessPriority;
		CfgWindowType.SelectedIndex = current.WindowType;
		CfgWindowWidth.Text = current.WindowWidth.ToString();
		CfgWindowHeight.Text = current.WindowHeight.ToString();
		UpdateWindowCustomVisibility();
		CfgRamAuto.IsChecked = current.RamType == 0;
		CfgRamCustom.IsChecked = current.RamType == 1;
		CfgRamSlider.IsEnabled = current.RamType == 1;
		CfgRamSlider.Value = current.MaxRamMb;
		CfgRamLabel.Text = $"{current.MaxRamMb} MB";
		CfgOptimizeMemory.IsChecked = current.OptimizeMemoryBeforeLaunch;
		SetSkinRadio(current.SkinType);
		CfgSkinId.Text = current.SkinId;
		UpdateSkinIdVisibility();
		CfgJvmArgs.Text = current.JvmArgs;
		CfgGameArgs.Text = current.GameArgs;
		CfgPreLaunch.Text = current.PreLaunchCommand;
		CfgWaitPreLaunch.IsChecked = current.WaitForPreLaunch;
		CfgGcType.SelectedIndex = current.GcType;
		CfgDisableJlw.IsChecked = current.DisableJlw;
		CfgDisableLua.IsChecked = current.DisableLua;
		CfgHighPerfGpu.IsChecked = current.UseHighPerfGpu;
		CfgOpacity.Value = current.Opacity;
		CfgHue.Value = current.Hue;
		CfgSaturation.Value = current.Saturation;
		CfgLightness.Value = current.Lightness;
		CfgHueDelta.Value = current.HueDelta;
		CfgShowLogo.IsChecked = current.ShowLogo;
		CfgLanguage.SelectedIndex = ((current.Language == "zh_CN") ? 1 : 0);
		InitThemePanel(current.Theme);
		CfgBackgroundFit.SelectedIndex = MapBackgroundFitIndex(current.BackgroundFit);
		CfgBackgroundOpacity.Value = current.BackgroundOpacity;
		CfgBackgroundBlur.Value = current.BackgroundBlur;
		CfgColorfulBackground.IsChecked = current.ColorfulBackground;
		CfgMusicVolume.Value = current.MusicVolume;
		CfgMusicRandom.IsChecked = current.MusicRandom;
		CfgMusicAuto.IsChecked = current.MusicAuto;
		CfgMusicStart.IsChecked = current.MusicStart;
		CfgMusicStop.IsChecked = current.MusicStop;
		SetLogoRadio(current.LogoType);
		CfgEnableAnimation.IsChecked = current.EnableAnimation;
		CfgAnimationSpeed.Value = current.AnimationSpeed;
		CfgAnimationSpeedLabel.Text = $"{current.AnimationSpeed}%";
		CfgLinkLatencyMode.SelectedIndex = current.LinkLatencyMode;
		CfgLinkCustomPeer.Text = current.LinkCustomPeer;
		CfgLinkPort.Text = current.LinkPort;
		CfgLinkMaxPlayers.Value = current.LinkMaxPlayers;
		CfgLinkHeartbeat.Value = current.LinkHeartbeat;
		CfgLinkHeartbeatLabel.Text = $"{current.LinkHeartbeat}s";
		CfgLinkTimeout.Value = current.LinkTimeout;
		CfgLinkTimeoutLabel.Text = $"{current.LinkTimeout}s";
		CfgLinkUpnp.IsChecked = current.LinkUpnp;
		CfgLinkCompress.IsChecked = current.LinkCompress;
		CfgLinkEncrypt.IsChecked = current.LinkEncrypt;
		CfgLinkRelayServer.SelectedIndex = current.LinkRelayServer;
		CfgLinkMtu.SelectedIndex = current.LinkMtu;
		CfgLinkAllowSpectator.IsChecked = current.LinkAllowSpectator;
		CfgLinkWhitelist.IsChecked = current.LinkWhitelist;
		CfgLinkAutoKick.IsChecked = current.LinkAutoKick;
		CfgLinkShowPing.IsChecked = current.LinkShowPing;
		CfgDownloadSource.SelectedIndex = current.DownloadSource;
		CfgVersionListSource.SelectedIndex = current.VersionListSource;
		CfgMaxThreads.Value = current.MaxThreads;
		CfgMaxThreadsLabel.Text = current.MaxThreads.ToString();
		CfgSpeedLimit.Value = current.SpeedLimit;
		CfgSpeedLimitLabel.Text = ((current.SpeedLimit >= 42) ? LanguageManager.Get("ResSpeedUnlimited") : string.Format(LanguageManager.Get("ResSpeedValue"), current.SpeedLimit));
		CfgVerifySsl.IsChecked = current.VerifySsl;
		CfgModSource.SelectedIndex = current.ModSource;
		CfgModNameFormat.SelectedIndex = current.ModNameFormat;
		CfgModLocalNameStyle.SelectedIndex = current.ModLocalNameStyle;
		CfgUpdateRelease.IsChecked = current.UpdateRelease;
		CfgUpdateSnapshot.IsChecked = current.UpdateSnapshot;
		CfgAutoChinese.IsChecked = current.AutoChinese;
		CfgAutoCheckUpdate.IsChecked = current.AutoCheckUpdate;
		CfgShowSnapshot.IsChecked = current.ShowDownloadSnapshot;
		CfgShowOldBeta.IsChecked = current.ShowDownloadOldBeta;
		CfgShowAprilFool.IsChecked = current.ShowDownloadAprilFool;
		_suppressConfigSave = false;
	}

	private void SaveSettingsFromControls()
	{
		if (!_suppressConfigSave)
		{
			LauncherConfigData current = LauncherConfig.Current;
			current.VersionIsolation = CfgVersionIsolation.SelectedIndex;
			current.WindowTitle = CfgWindowTitle.Text;
			current.CustomInfo = CfgCustomInfo.Text;
			current.LauncherVisibility = CfgLauncherVisibility.SelectedIndex;
			current.ProcessPriority = CfgProcessPriority.SelectedIndex;
			current.WindowType = CfgWindowType.SelectedIndex;
			if (int.TryParse(CfgWindowWidth.Text, out var result))
			{
				current.WindowWidth = result;
			}
			if (int.TryParse(CfgWindowHeight.Text, out var result2))
			{
				current.WindowHeight = result2;
			}
			current.RamType = (CfgRamCustom.IsChecked.GetValueOrDefault() ? 1 : 0);
			current.MaxRamMb = (int)CfgRamSlider.Value;
			current.OptimizeMemoryBeforeLaunch = CfgOptimizeMemory.IsChecked.GetValueOrDefault();
			current.SkinType = GetSkinRadio();
			current.SkinId = CfgSkinId.Text;
			current.JvmArgs = CfgJvmArgs.Text;
			current.GameArgs = CfgGameArgs.Text;
			current.PreLaunchCommand = CfgPreLaunch.Text;
			current.WaitForPreLaunch = CfgWaitPreLaunch.IsChecked.GetValueOrDefault();
			current.GcType = CfgGcType.SelectedIndex;
			current.DisableJlw = CfgDisableJlw.IsChecked.GetValueOrDefault();
			current.DisableLua = CfgDisableLua.IsChecked.GetValueOrDefault();
			current.UseHighPerfGpu = CfgHighPerfGpu.IsChecked.GetValueOrDefault();
			current.Opacity = (int)CfgOpacity.Value;
			current.Hue = (int)CfgHue.Value;
			current.Saturation = (int)CfgSaturation.Value;
			current.Lightness = (int)CfgLightness.Value;
			current.HueDelta = (int)CfgHueDelta.Value;
			current.ShowLogo = CfgShowLogo.IsChecked.GetValueOrDefault();
			current.Language = ((CfgLanguage.SelectedIndex == 1) ? "zh_CN" : "en_US");
			current.BackgroundFit = MapBackgroundFitValue(CfgBackgroundFit.SelectedIndex);
			current.BackgroundOpacity = (int)CfgBackgroundOpacity.Value;
			current.BackgroundBlur = (int)CfgBackgroundBlur.Value;
			current.ColorfulBackground = CfgColorfulBackground.IsChecked.GetValueOrDefault();
			current.MusicVolume = (int)CfgMusicVolume.Value;
			current.MusicRandom = CfgMusicRandom.IsChecked.GetValueOrDefault();
			current.MusicAuto = CfgMusicAuto.IsChecked.GetValueOrDefault();
			current.MusicStart = CfgMusicStart.IsChecked.GetValueOrDefault();
			current.MusicStop = CfgMusicStop.IsChecked.GetValueOrDefault();
			current.LogoType = GetLogoRadio();
			current.EnableAnimation = CfgEnableAnimation.IsChecked.GetValueOrDefault();
			current.AnimationSpeed = (int)CfgAnimationSpeed.Value;
			current.LinkLatencyMode = CfgLinkLatencyMode.SelectedIndex;
			current.LinkCustomPeer = CfgLinkCustomPeer.Text;
			current.LinkPort = CfgLinkPort.Text;
			current.LinkMaxPlayers = (int)CfgLinkMaxPlayers.Value;
			current.LinkHeartbeat = (int)CfgLinkHeartbeat.Value;
			current.LinkTimeout = (int)CfgLinkTimeout.Value;
			current.LinkUpnp = CfgLinkUpnp.IsChecked.GetValueOrDefault();
			current.LinkCompress = CfgLinkCompress.IsChecked.GetValueOrDefault();
			current.LinkEncrypt = CfgLinkEncrypt.IsChecked.GetValueOrDefault();
			current.LinkRelayServer = CfgLinkRelayServer.SelectedIndex;
			current.LinkMtu = CfgLinkMtu.SelectedIndex;
			current.LinkAllowSpectator = CfgLinkAllowSpectator.IsChecked.GetValueOrDefault();
			current.LinkWhitelist = CfgLinkWhitelist.IsChecked.GetValueOrDefault();
			current.LinkAutoKick = CfgLinkAutoKick.IsChecked.GetValueOrDefault();
			current.LinkShowPing = CfgLinkShowPing.IsChecked.GetValueOrDefault();
			current.DownloadSource = CfgDownloadSource.SelectedIndex;
			current.VersionListSource = CfgVersionListSource.SelectedIndex;
			current.MaxThreads = (int)CfgMaxThreads.Value;
			current.SpeedLimit = (int)CfgSpeedLimit.Value;
			current.VerifySsl = CfgVerifySsl.IsChecked.GetValueOrDefault();
			current.ModSource = CfgModSource.SelectedIndex;
			current.ModNameFormat = CfgModNameFormat.SelectedIndex;
			current.ModLocalNameStyle = CfgModLocalNameStyle.SelectedIndex;
			current.UpdateRelease = CfgUpdateRelease.IsChecked.GetValueOrDefault();
			current.UpdateSnapshot = CfgUpdateSnapshot.IsChecked.GetValueOrDefault();
			current.AutoChinese = CfgAutoChinese.IsChecked.GetValueOrDefault();
			current.AutoCheckUpdate = CfgAutoCheckUpdate.IsChecked.GetValueOrDefault();
			current.ShowDownloadSnapshot = CfgShowSnapshot.IsChecked.GetValueOrDefault();
			current.ShowDownloadOldBeta = CfgShowOldBeta.IsChecked.GetValueOrDefault();
			current.ShowDownloadAprilFool = CfgShowAprilFool.IsChecked.GetValueOrDefault();
			LauncherConfig.Save();
		}
	}

	private int GetSkinRadio()
	{
		if (CfgSkin0.IsChecked.GetValueOrDefault())
		{
			return 0;
		}
		if (CfgSkin1.IsChecked.GetValueOrDefault())
		{
			return 1;
		}
		if (CfgSkin2.IsChecked.GetValueOrDefault())
		{
			return 2;
		}
		if (CfgSkin3.IsChecked.GetValueOrDefault())
		{
			return 3;
		}
		if (CfgSkin4.IsChecked.GetValueOrDefault())
		{
			return 4;
		}
		return 0;
	}

	private int GetLogoRadio()
	{
		if (CfgLogo0.IsChecked.GetValueOrDefault())
		{
			return 0;
		}
		if (CfgLogo1.IsChecked.GetValueOrDefault())
		{
			return 1;
		}
		if (CfgLogo2.IsChecked.GetValueOrDefault())
		{
			return 2;
		}
		if (CfgLogo3.IsChecked.GetValueOrDefault())
		{
			return 3;
		}
		return 1;
	}

	private void SetSkinRadio(int type)
	{
		CfgSkin0.IsChecked = type == 0;
		CfgSkin1.IsChecked = type == 1;
		CfgSkin2.IsChecked = type == 2;
		CfgSkin3.IsChecked = type == 3;
		CfgSkin4.IsChecked = type == 4;
	}

	private void SetLogoRadio(int type)
	{
		CfgLogo0.IsChecked = type == 0;
		CfgLogo1.IsChecked = type == 1;
		CfgLogo2.IsChecked = type == 2;
		CfgLogo3.IsChecked = type == 3;
	}

	private void UpdateSkinIdVisibility()
	{
		bool valueOrDefault = CfgSkin3.IsChecked.GetValueOrDefault();
		CfgSkinIdPanel.Visibility = ((!valueOrDefault) ? Visibility.Collapsed : Visibility.Visible);
	}

	private void UpdateWindowCustomVisibility()
	{
		bool flag = CfgWindowType.SelectedIndex == 3;
		CfgWindowWidth.Visibility = ((!flag) ? Visibility.Collapsed : Visibility.Visible);
		CfgWindowHeight.Visibility = ((!flag) ? Visibility.Collapsed : Visibility.Visible);
		CfgWindowX.Visibility = ((!flag) ? Visibility.Collapsed : Visibility.Visible);
	}

	private static int MapBackgroundFitIndex(int fit)
	{
		if (1 == 0)
		{
		}
		int result = fit switch
		{
			0 => 0, 
			4 => 1, 
			1 => 2, 
			3 => 3, 
			2 => 4, 
			_ => 0, 
		};
		if (1 == 0)
		{
		}
		return result;
	}

	private static int MapBackgroundFitValue(int index)
	{
		if (1 == 0)
		{
		}
		int result = index switch
		{
			0 => 0, 
			1 => 4, 
			2 => 1, 
			3 => 3, 
			4 => 2, 
			_ => 0, 
		};
		if (1 == 0)
		{
		}
		return result;
	}

	private void OnConfigChanged(object? sender, EventArgs e)
	{
		base.Dispatcher.BeginInvoke((Action)delegate
		{
			ApplyConfigToLaunchPage();
			ApplyPersonalization();
		});
	}

	private void ShowSettingsPage()
	{
		SwitchToPage(SettingsPage, SettingsPageSlide, delegate
		{
			if (_settingsInitialized)
			{
				InitSettingsControls();
			}
			string initialTab = (_selectedSettingsTab?.Tag as string) ?? "launch";
			UpdateSettingsTabVisibility(initialTab);
			base.Dispatcher.BeginInvoke((Action)delegate
			{
				AnimateSettingsCards(initialTab);
			}, DispatcherPriority.Loaded);
		});
	}

	private void UpdateSettingsTabVisibility(string key)
	{
		SettingsTabLaunch.Visibility = ((!(key == "launch")) ? Visibility.Collapsed : Visibility.Visible);
		SettingsTabUI.Visibility = ((!(key == "ui")) ? Visibility.Collapsed : Visibility.Visible);
		SettingsTabLink.Visibility = ((!(key == "link")) ? Visibility.Collapsed : Visibility.Visible);
		SettingsTabSystem.Visibility = ((!(key == "system")) ? Visibility.Collapsed : Visibility.Visible);
	}

	private void InitSettingsTabs()
	{
		if (SettingsTabPanel.Children.Count > 0)
		{
			return;
		}
		string[] array = new string[4] { "SettingsTabLaunch", "SettingsTabUI", "SettingsTabLink", "SettingsTabSystem" };
		for (int i = 0; i < SettingsTabKeys.Length; i++)
		{
			Border border = new Border
			{
				Tag = SettingsTabKeys[i],
				Height = 28.0,
				CornerRadius = new CornerRadius(6.0),
				Padding = new Thickness(14.0, 0.0, 14.0, 0.0),
				Margin = new Thickness(0.0, 0.0, 6.0, 0.0),
				Background = Brushes.Transparent,
				Cursor = Cursors.Hand,
				RenderTransformOrigin = new Point(0.5, 0.5),
				RenderTransform = new ScaleTransform(1.0, 1.0)
			};
			TextBlock textBlock = new TextBlock
			{
				Foreground = new SolidColorBrush(Color.FromRgb(170, 170, 170)),
				FontSize = 12.0,
				VerticalAlignment = VerticalAlignment.Center,
				HorizontalAlignment = HorizontalAlignment.Center
			};
			textBlock.SetResourceReference(TextBlock.TextProperty, array[i]);
			border.Child = textBlock;
			border.MouseEnter += SettingsTab_MouseEnter;
			border.MouseLeave += SettingsTab_MouseLeave;
			border.MouseLeftButtonUp += SettingsTab_Click;
			SettingsTabPanel.Children.Add(border);
			if (i == 0)
			{
				_selectedSettingsTab = border;
				border.Background = new SolidColorBrush(Color.FromRgb(72, 144, 245));
				textBlock.Foreground = Brushes.White;
			}
		}
		if (CfgPathLabel != null)
		{
			CfgPathLabel.Text = LauncherConfig.ConfigFilePath;
		}
		AttachSettingsHandlers(SettingsTabLaunch);
		AttachSettingsHandlers(SettingsTabUI);
		AttachSettingsHandlers(SettingsTabLink);
		AttachSettingsHandlers(SettingsTabSystem);
		_settingsInitialized = true;
	}

	private void AttachSettingsHandlers(DependencyObject root)
	{
		int childrenCount = VisualTreeHelper.GetChildrenCount(root);
		for (int i = 0; i < childrenCount; i++)
		{
			DependencyObject child = VisualTreeHelper.GetChild(root, i);
			if (child is ComboBox comboBox)
			{
				comboBox.SelectionChanged += CfgGeneric_Changed;
				if (comboBox == CfgWindowType)
				{
					comboBox.SelectionChanged += CfgWindowType_Changed;
				}
			}
			else if (child is CheckBox checkBox)
			{
				checkBox.Checked += CfgGeneric_Changed;
				checkBox.Unchecked += CfgGeneric_Changed;
			}
			else if (child is RadioButton radioButton)
			{
				radioButton.Checked += CfgGeneric_Changed;
				if (radioButton == CfgSkin3)
				{
					radioButton.Checked += CfgSkin_Changed;
				}
			}
			else if (child is TextBox textBox)
			{
				textBox.LostFocus += CfgGeneric_Changed;
			}
			else if (child is Slider slider && slider != CfgRamSlider && slider != CfgOpacity && slider != CfgHue && slider != CfgSaturation && slider != CfgLightness && slider != CfgHueDelta && slider != CfgAnimationSpeed)
			{
				slider.ValueChanged += CfgGenericSlider_Changed;
			}
			AttachSettingsHandlers(child);
		}
	}

	private void CfgGeneric_Changed(object sender, RoutedEventArgs e)
	{
		if (!_suppressConfigSave && _settingsInitialized)
		{
			SaveSettingsFromControls();
		}
	}

	private void CfgGenericSlider_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
	{
		if (!_suppressConfigSave && _settingsInitialized)
		{
			if (sender == CfgMaxThreads && CfgMaxThreadsLabel != null)
			{
				CfgMaxThreadsLabel.Text = ((int)CfgMaxThreads.Value).ToString();
			}
			else if (sender == CfgSpeedLimit && CfgSpeedLimitLabel != null)
			{
				CfgSpeedLimitLabel.Text = ((CfgSpeedLimit.Value >= 42.0) ? LanguageManager.Get("ResSpeedUnlimited") : string.Format(LanguageManager.Get("ResSpeedValue"), (int)CfgSpeedLimit.Value));
			}
			else if (sender == CfgLinkHeartbeat && CfgLinkHeartbeatLabel != null)
			{
				CfgLinkHeartbeatLabel.Text = $"{(int)CfgLinkHeartbeat.Value}s";
			}
			else if (sender == CfgLinkTimeout && CfgLinkTimeoutLabel != null)
			{
				CfgLinkTimeoutLabel.Text = $"{(int)CfgLinkTimeout.Value}s";
			}
			else if (sender == CfgBackgroundOpacity)
			{
				LauncherConfig.Current.BackgroundOpacity = (int)CfgBackgroundOpacity.Value;
			}
			else if (sender == CfgBackgroundBlur)
			{
				LauncherConfig.Current.BackgroundBlur = (int)CfgBackgroundBlur.Value;
			}
			else if (sender == CfgMusicVolume)
			{
				LauncherConfig.Current.MusicVolume = (int)CfgMusicVolume.Value;
			}
			SaveSettingsFromControls();
		}
	}

	private void CfgWindowType_Changed(object sender, SelectionChangedEventArgs e)
	{
		UpdateWindowCustomVisibility();
	}

	private void CfgSkin_Changed(object sender, RoutedEventArgs e)
	{
		UpdateSkinIdVisibility();
	}

	private void SettingsTab_MouseEnter(object sender, MouseEventArgs e)
	{
		if (sender is Border border && border != _selectedSettingsTab)
		{
			border.Background = new SolidColorBrush(Color.FromRgb(50, 50, 54));
			if (border.Child is TextBlock textBlock)
			{
				textBlock.Foreground = Brushes.White;
			}
		}
	}

	private void SettingsTab_MouseLeave(object sender, MouseEventArgs e)
	{
		if (sender is Border border && border != _selectedSettingsTab)
		{
			border.Background = Brushes.Transparent;
			if (border.Child is TextBlock textBlock)
			{
				textBlock.Foreground = new SolidColorBrush(Color.FromRgb(170, 170, 170));
			}
		}
	}

	private void SettingsTab_Click(object sender, MouseButtonEventArgs e)
	{
		if (!(sender is Border { Tag: string tag } border))
		{
			return;
		}
		foreach (object child in SettingsTabPanel.Children)
		{
			if (child is Border border2)
			{
				bool flag = border2 == border;
				border2.Background = (flag ? new SolidColorBrush(_currentThemeColor) : Brushes.Transparent);
				if (border2.Child is TextBlock textBlock)
				{
					textBlock.Foreground = (flag ? Brushes.White : new SolidColorBrush(Color.FromRgb(170, 170, 170)));
				}
			}
		}
		_selectedSettingsTab = border;
		UpdateSettingsTabVisibility(tag);
		AnimateSettingsCards(tag);
	}

	private void AnimateSettingsCards(string key)
	{
		if (1 == 0)
		{
		}
		ScrollViewer scrollViewer = key switch
		{
			"launch" => SettingsTabLaunch, 
			"ui" => SettingsTabUI, 
			"link" => SettingsTabLink, 
			"system" => SettingsTabSystem, 
			_ => null, 
		};
		if (1 == 0)
		{
		}
		if (!(scrollViewer?.Content is StackPanel stackPanel))
		{
			return;
		}
		CubicEase easingFunction = new CubicEase
		{
			EasingMode = EasingMode.EaseOut
		};
		TimeSpan timeSpan = AnimDuration(360.0);
		int num = 0;
		foreach (object child in stackPanel.Children)
		{
			if (child is Border border)
			{
				TranslateTransform translateTransform = border.RenderTransform as TranslateTransform;
				if (translateTransform == null)
				{
					translateTransform = (TranslateTransform)(border.RenderTransform = new TranslateTransform());
				}
				translateTransform.BeginAnimation(TranslateTransform.YProperty, null);
				border.BeginAnimation(UIElement.OpacityProperty, null);
				translateTransform.Y = 16.0;
				border.Opacity = 0.0;
				DoubleAnimation animation = new DoubleAnimation(16.0, 0.0, timeSpan)
				{
					EasingFunction = easingFunction,
					BeginTime = TimeSpan.FromMilliseconds(num, 0L)
				};
				DoubleAnimation animation2 = new DoubleAnimation(0.0, 1.0, timeSpan)
				{
					EasingFunction = easingFunction,
					BeginTime = TimeSpan.FromMilliseconds(num, 0L)
				};
				translateTransform.BeginAnimation(TranslateTransform.YProperty, animation);
				border.BeginAnimation(UIElement.OpacityProperty, animation2);
				num += 60;
				if (num > 360)
				{
					num = 360;
				}
			}
		}
	}

	private void InitThemePanel(int selectedTheme)
	{
		if (CfgThemePanel.Children.Count > 0)
		{
			return;
		}
		for (int i = 0; i < ThemeNames.Length; i++)
		{
			Color themeColor = LauncherConfig.GetThemeColor(i);
			Border border = new Border
			{
				Width = 28.0,
				Height = 28.0,
				CornerRadius = new CornerRadius(14.0),
				Margin = new Thickness(0.0, 0.0, 8.0, 8.0),
				Cursor = Cursors.Hand,
				Tag = i,
				Background = new SolidColorBrush(themeColor),
				RenderTransformOrigin = new Point(0.5, 0.5),
				RenderTransform = new ScaleTransform(1.0, 1.0),
				ToolTip = ThemeNames[i]
			};
			if (i == selectedTheme)
			{
				border.BorderBrush = Brushes.White;
				border.BorderThickness = new Thickness(2.0);
			}
			border.MouseEnter += ThemeSwatch_MouseEnter;
			border.MouseLeave += ThemeSwatch_MouseLeave;
			border.MouseLeftButtonUp += ThemeSwatch_Click;
			CfgThemePanel.Children.Add(border);
		}
	}

	private void ThemeSwatch_MouseEnter(object sender, MouseEventArgs e)
	{
		if (sender is Border { RenderTransform: ScaleTransform renderTransform })
		{
			renderTransform.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(1.15, TimeSpan.FromMilliseconds(150L, 0L)));
			renderTransform.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(1.15, TimeSpan.FromMilliseconds(150L, 0L)));
		}
	}

	private void ThemeSwatch_MouseLeave(object sender, MouseEventArgs e)
	{
		if (sender is Border { RenderTransform: ScaleTransform renderTransform })
		{
			renderTransform.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(1.0, TimeSpan.FromMilliseconds(150L, 0L)));
			renderTransform.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(1.0, TimeSpan.FromMilliseconds(150L, 0L)));
		}
	}

	private void ThemeSwatch_Click(object sender, MouseButtonEventArgs e)
	{
		if (!(sender is Border { Tag: var tag }) || !(tag is int num))
		{
			return;
		}
		foreach (object child in CfgThemePanel.Children)
		{
			if (child is Border border2)
			{
				border2.BorderBrush = ((border2.Tag is int num2 && num2 == num) ? Brushes.White : null);
				border2.BorderThickness = ((border2.Tag is int num3 && num3 == num) ? new Thickness(2.0) : new Thickness(0.0));
			}
		}
		LauncherConfig.Current.Theme = num;
		LauncherConfig.Save();
		ApplyPersonalization();
	}
}