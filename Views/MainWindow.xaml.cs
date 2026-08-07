using System;
using System.CodeDom.Compiler;
using System.ComponentModel;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using DynamicIsland.Services;

namespace DynamicIsland;

public partial class MainWindow : Window
{
	private const double SmallWidth = 160.0;

	private const double SmallHeight = 40.0;

	private const double HoverWidth = 220.0;

	private const double WideWidth = 900.0;

	private const double WideHeight = 45.0;

	private const double ExpandedHeight = 500.0;

	private const double TopMargin = 10.0;

	private const double RadiusSmall = 20.0;

	private const double RadiusWide = 22.5;

	private const double RadiusExpanded = 28.0;

	public static readonly DependencyProperty PillRadiusProperty = DependencyProperty.Register("PillRadius", typeof(double), typeof(MainWindow), new PropertyMetadata(20.0, OnPillRadiusChanged));

	private int _state = 0;

	private bool _isAnimating = false;

	private bool _isShuttingDown = false;

	private bool _isDragging = false;

	private Point _dragStart;

	private double _dragDeltaY;

	private double _cachedCenterX;

	private SizeChangedEventHandler? _centerSyncHandler;

	private readonly DispatcherTimer _clockTimer;

	private bool _isShowingProgress = false;

	private bool _isHovering = false;

	private DateTime _lastProgressUpdate = DateTime.MinValue;

	private bool _skeletonCompleted = false;

	private LauncherPanel? _launcherPanel;

	private DispatcherTimer? _notificationTimer;

	private bool _isShowingNotification = false;

	private bool _notificationWasProgress = false;

	public double PillRadius
	{
		get
		{
			return (double)GetValue(PillRadiusProperty);
		}
		set
		{
			SetValue(PillRadiusProperty, value);
		}
	}

	private static void OnPillRadiusChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
	{
		if (d is MainWindow mainWindow)
		{
			double uniformRadius = (double)e.NewValue;
			mainWindow.MainPill.CornerRadius = new CornerRadius(uniformRadius);
		}
	}

	public MainWindow()
	{
		InitializeComponent();
		StartSkeletonLoading();
		_clockTimer = new DispatcherTimer
		{
			Interval = TimeSpan.FromSeconds(1L)
		};
		_clockTimer.Tick += UpdateClock;
		_clockTimer.Start();
		UpdateClock(null, EventArgs.Empty);
		DownloadManager.ProgressChanged += OnDownloadProgressChanged;
		DownloadManager.DownloadCompleted += OnDownloadCompleted;
		DownloadManager.DownloadFailed += OnDownloadFailed;
		NotificationManager.Requested += OnNotificationRequested;
		base.Deactivated += MainWindow_Deactivated;
		base.Loaded += MainWindow_Loaded;
		InitializeAsync();
	}

	private async Task InitializeAsync()
	{
		await Task.Delay(100);
		await base.Dispatcher.BeginInvoke((Action)delegate
		{
			try
			{
				LauncherConfig.Load();
				VersionSettingsManager.Load();
				LanguageManager.Apply(LauncherConfig.Current.Language);
			}
			catch (Exception)
			{
			}
		});
	}

	private void MainWindow_Deactivated(object? sender, EventArgs e)
	{
		if (_state != 2 || _isAnimating)
		{
			return;
		}
		base.Dispatcher.BeginInvoke((Action)delegate
		{
			if (_state == 2 && !_isAnimating)
			{
				TransitionToSmall();
			}
		}, DispatcherPriority.Background);
	}

	private void MainWindow_Loaded(object sender, RoutedEventArgs e)
	{
		try
		{
			WindowHelper.SetupAsOverlay(this);
			NativeBackdrop.ApplyDarkMode(this);
			PositionWindow();
			PlayStartupAnimation();
			base.Dispatcher.BeginInvoke((Action)delegate
			{
				DispatcherTimer minTimer = new DispatcherTimer
				{
					Interval = TimeSpan.FromMilliseconds(500L, 0L)
				};
				minTimer.Tick += delegate
				{
					minTimer.Stop();
					CompleteSkeletonLoading();
				};
				minTimer.Start();
			}, DispatcherPriority.ApplicationIdle);
			if (!LauncherConfig.Current.AutoCheckUpdate)
			{
				return;
			}
			Task.Run(async delegate
			{
				await Task.Delay(3000);
				await base.Dispatcher.BeginInvoke((Action)async delegate
				{
					await UpdateChecker.CheckAsync();
				});
			});
		}
		catch (Exception)
		{
		}
	}

	private void UpdateClock(object? sender, EventArgs e)
	{
		string text = DateTime.Now.ToString("HH:mm");
		TimeText.Text = text;
		HoverTimeText.Text = text;
	}

	private void StartSkeletonLoading()
	{
		SkeletonBar.Visibility = Visibility.Visible;
		SkeletonBar.Opacity = 1.0;
		SkeletonTranslate.X = 0.0;
		TimeText.Opacity = 0.0;
		TimeTranslate.X = -50.0;
		DoubleAnimation animation = new DoubleAnimation(-24.0, 58.0, TimeSpan.FromMilliseconds(900L, 0L))
		{
			RepeatBehavior = RepeatBehavior.Forever,
			EasingFunction = new SineEase
			{
				EasingMode = EasingMode.EaseInOut
			}
		};
		SkeletonShineTranslate.BeginAnimation(TranslateTransform.XProperty, animation);
	}

	private void CompleteSkeletonLoading()
	{
		if (!_skeletonCompleted)
		{
			_skeletonCompleted = true;
			SkeletonShineTranslate.BeginAnimation(TranslateTransform.XProperty, null);
			DoubleAnimation animation = new DoubleAnimation(-50.0, 0.0, TimeSpan.FromMilliseconds(450L, 0L))
			{
				EasingFunction = new CubicEase
				{
					EasingMode = EasingMode.EaseOut
				}
			};
			DoubleAnimation animation2 = new DoubleAnimation(0.0, 1.0, TimeSpan.FromMilliseconds(300L, 0L));
			TimeTranslate.BeginAnimation(TranslateTransform.XProperty, animation);
			TimeText.BeginAnimation(UIElement.OpacityProperty, animation2);
			DoubleAnimation animation3 = new DoubleAnimation(0.0, 50.0, TimeSpan.FromMilliseconds(450L, 0L))
			{
				EasingFunction = new CubicEase
				{
					EasingMode = EasingMode.EaseOut
				}
			};
			DoubleAnimation doubleAnimation = new DoubleAnimation(1.0, 0.0, TimeSpan.FromMilliseconds(400L, 0L));
			doubleAnimation.Completed += delegate
			{
				SkeletonBar.Visibility = Visibility.Collapsed;
			};
			SkeletonTranslate.BeginAnimation(TranslateTransform.XProperty, animation3);
			SkeletonBar.BeginAnimation(UIElement.OpacityProperty, doubleAnimation);
			base.Dispatcher.BeginInvoke((Action)delegate
			{
				EnsureLauncherPanelCreated();
			}, DispatcherPriority.Background);
		}
	}

	private LauncherPanel EnsureLauncherPanelCreated()
	{
		if (_launcherPanel != null)
		{
			return _launcherPanel;
		}
		_launcherPanel = new LauncherPanel
		{
			Opacity = 0.0,
			Visibility = Visibility.Collapsed
		};
		_launcherPanel.CollapseRequested += delegate
		{
			TransitionToSmall();
		};
		_launcherPanel.ExitRequested += delegate
		{
			Application.Current.Shutdown();
		};
		ExpandedContainer.Children.Add(_launcherPanel);
		return _launcherPanel;
	}

	private void OnDownloadProgressChanged(DownloadTask task)
	{
		DownloadTask task2 = task;
		base.Dispatcher.BeginInvoke((Action)delegate
		{
			if (_state == 0 && !_isAnimating)
			{
				DateTime now = DateTime.Now;
				if (!((now - _lastProgressUpdate).TotalMilliseconds < 120.0))
				{
					_lastProgressUpdate = now;
					DownloadProgress.Value = task2.Progress;
					HoverPercentText.Text = $"{(int)task2.Progress}%";
					if (!_isShowingProgress)
					{
						ShowProgressBar();
					}
				}
			}
		}, DispatcherPriority.Background);
	}

	private void OnDownloadCompleted(DownloadTask task)
	{
		base.Dispatcher.Invoke(delegate
		{
			DownloadProgress.Value = 100.0;
			HoverPercentText.Text = "100%";
			DispatcherTimer timer = new DispatcherTimer
			{
				Interval = TimeSpan.FromMilliseconds(600L, 0L)
			};
			timer.Tick += delegate
			{
				timer.Stop();
				HideProgressBar();
			};
			timer.Start();
		});
	}

	private void OnDownloadFailed(DownloadTask task, Exception ex)
	{
		base.Dispatcher.Invoke(delegate
		{
			DispatcherTimer timer = new DispatcherTimer
			{
				Interval = TimeSpan.FromMilliseconds(1200L, 0L)
			};
			timer.Tick += delegate
			{
				timer.Stop();
				HideProgressBar();
			};
			timer.Start();
		});
	}

	private void ShowProgressBar()
	{
		if (!_isShowingProgress && _state == 0)
		{
			_isShowingProgress = true;
			CubicEase cubicEase = new CubicEase
			{
				EasingMode = EasingMode.EaseOut
			};
			DoubleAnimation animation = new DoubleAnimation(0.0, 40.0, TimeSpan.FromMilliseconds(300L, 0L))
			{
				EasingFunction = cubicEase
			};
			DoubleAnimation doubleAnimation = new DoubleAnimation(1.0, 0.0, TimeSpan.FromMilliseconds(250L, 0L))
			{
				EasingFunction = cubicEase
			};
			doubleAnimation.Completed += delegate
			{
				ClockContainer.Visibility = Visibility.Collapsed;
				ProgressContainer.Visibility = Visibility.Visible;
				ProgressTranslate.BeginAnimation(TranslateTransform.XProperty, null);
				ProgressTranslate.X = -40.0;
				ProgressContainer.Opacity = 0.0;
				DoubleAnimation animation2 = new DoubleAnimation(-40.0, 0.0, TimeSpan.FromMilliseconds(350L, 0L))
				{
					EasingFunction = cubicEase
				};
				DoubleAnimation animation3 = new DoubleAnimation(0.0, 1.0, TimeSpan.FromMilliseconds(300L, 0L))
				{
					EasingFunction = cubicEase
				};
				ProgressTranslate.BeginAnimation(TranslateTransform.XProperty, animation2);
				ProgressContainer.BeginAnimation(UIElement.OpacityProperty, animation3);
			};
			TimeTranslate.BeginAnimation(TranslateTransform.XProperty, animation);
			TimeText.BeginAnimation(UIElement.OpacityProperty, doubleAnimation);
		}
	}

	private void HideProgressBar()
	{
		if (_isShowingProgress)
		{
			_isShowingProgress = false;
			CubicEase cubicEase = new CubicEase
			{
				EasingMode = EasingMode.EaseOut
			};
			DoubleAnimation animation = new DoubleAnimation(0.0, 40.0, TimeSpan.FromMilliseconds(300L, 0L))
			{
				EasingFunction = cubicEase
			};
			DoubleAnimation doubleAnimation = new DoubleAnimation(1.0, 0.0, TimeSpan.FromMilliseconds(250L, 0L))
			{
				EasingFunction = cubicEase
			};
			doubleAnimation.Completed += delegate
			{
				ProgressContainer.Visibility = Visibility.Collapsed;
				ClockContainer.Visibility = Visibility.Visible;
				TimeTranslate.BeginAnimation(TranslateTransform.XProperty, null);
				TimeTranslate.X = -40.0;
				TimeText.BeginAnimation(UIElement.OpacityProperty, null);
				TimeText.Opacity = 0.0;
				DoubleAnimation animation2 = new DoubleAnimation(-40.0, 0.0, TimeSpan.FromMilliseconds(350L, 0L))
				{
					EasingFunction = cubicEase
				};
				DoubleAnimation animation3 = new DoubleAnimation(0.0, 1.0, TimeSpan.FromMilliseconds(300L, 0L))
				{
					EasingFunction = cubicEase
				};
				TimeTranslate.BeginAnimation(TranslateTransform.XProperty, animation2);
				TimeText.BeginAnimation(UIElement.OpacityProperty, animation3);
			};
			ProgressTranslate.BeginAnimation(TranslateTransform.XProperty, animation);
			ProgressContainer.BeginAnimation(UIElement.OpacityProperty, doubleAnimation);
		}
	}

	private void UpdateHoverState()
	{
		if (_state == 0)
		{
			CubicEase easingFunction = new CubicEase
			{
				EasingMode = EasingMode.EaseOut
			};
			TimeSpan timeSpan = TimeSpan.FromMilliseconds(200L, 0L);
			if (_isHovering && _isShowingProgress)
			{
				DownloadProgress.BeginAnimation(FrameworkElement.WidthProperty, new DoubleAnimation(80.0, timeSpan)
				{
					EasingFunction = easingFunction
				});
				HoverTimeText.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(1.0, timeSpan)
				{
					EasingFunction = easingFunction
				});
				HoverPercentText.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(1.0, timeSpan)
				{
					EasingFunction = easingFunction
				});
			}
			else if (_isShowingProgress)
			{
				DownloadProgress.BeginAnimation(FrameworkElement.WidthProperty, new DoubleAnimation(120.0, timeSpan)
				{
					EasingFunction = easingFunction
				});
				HoverTimeText.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0.0, timeSpan)
				{
					EasingFunction = easingFunction
				});
				HoverPercentText.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0.0, timeSpan)
				{
					EasingFunction = easingFunction
				});
			}
		}
	}

	private void PositionWindow()
	{
		Rect screenBounds = WindowHelper.GetScreenBounds(this);
		base.Width = 220.0;
		base.Height = 40.0;
		base.Left = screenBounds.X + (screenBounds.Width - 220.0) / 2.0;
		base.Top = screenBounds.Y + 10.0;
	}

	private double ScreenCenterX()
	{
		Rect screenBounds = WindowHelper.GetScreenBounds(this);
		return screenBounds.X + screenBounds.Width / 2.0;
	}

	private void PlayStartupAnimation()
	{
		double targetTop = base.Top;
		double top = WindowHelper.GetScreenBounds(this).Y - 40.0 - 30.0;
		base.Top = top;
		BackEase easingFunction = new BackEase
		{
			Amplitude = 0.3,
			EasingMode = EasingMode.EaseOut
		};
		DoubleAnimation doubleAnimation = new DoubleAnimation(targetTop, TimeSpan.FromMilliseconds(600L, 0L))
		{
			EasingFunction = easingFunction
		};
		doubleAnimation.Completed += delegate
		{
			BeginAnimation(Window.TopProperty, null);
			base.Top = targetTop;
		};
		BeginAnimation(Window.TopProperty, doubleAnimation);
	}

	private void PlayShutdownAnimation()
	{
		double toValue = WindowHelper.GetScreenBounds(this).Y - base.ActualHeight - 30.0;
		CubicEase easingFunction = new CubicEase
		{
			EasingMode = EasingMode.EaseIn
		};
		DoubleAnimation doubleAnimation = new DoubleAnimation(toValue, TimeSpan.FromMilliseconds(450L, 0L))
		{
			EasingFunction = easingFunction
		};
		doubleAnimation.Completed += delegate
		{
			Application.Current.Shutdown();
		};
		BeginAnimation(Window.TopProperty, doubleAnimation);
	}

	protected override void OnClosing(CancelEventArgs e)
	{
		base.OnClosing(e);
		if (!e.Cancel && !_isShuttingDown)
		{
			e.Cancel = true;
			_isShuttingDown = true;
			PlayShutdownAnimation();
		}
	}

	private void OnNotificationRequested(string message)
	{
		string message2 = message;
		base.Dispatcher.BeginInvoke((Action)delegate
		{
			ShowNotification(message2);
		});
	}

	private void ShowNotification(string message)
	{
		NotificationText.Text = message;
		if (_isShowingNotification)
		{
			_notificationTimer?.Stop();
			_notificationTimer?.Start();
			return;
		}
		_isShowingNotification = true;
		if (_state == 0)
		{
			ShowNotificationSmall();
		}
		else
		{
			ShowNotificationExpanded();
		}
	}

	private void ShowNotificationSmall()
	{
		_notificationWasProgress = _isShowingProgress;
		CubicEase easingFunction = new CubicEase
		{
			EasingMode = EasingMode.EaseOut
		};
		TimeSpan timeSpan = TimeSpan.FromMilliseconds(300L, 0L);
		TimeText.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(TimeText.Opacity, 0.0, TimeSpan.FromMilliseconds(150L, 0L))
		{
			EasingFunction = easingFunction
		});
		if (_isShowingProgress)
		{
			ProgressContainer.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(1.0, 0.0, TimeSpan.FromMilliseconds(150L, 0L))
			{
				EasingFunction = easingFunction
			});
		}
		NotificationContainer.Visibility = Visibility.Visible;
		NotificationContainer.VerticalAlignment = VerticalAlignment.Center;
		NotificationContainer.Margin = new Thickness(0.0);
		NotificationText.Opacity = 0.0;
		NotificationTranslate.Y = 0.0;
		NotificationText.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
		double width = NotificationText.DesiredSize.Width;
		double num = Math.Min(Math.Max(width + 60.0, 200.0), 560.0);
		double num2 = num + 20.0;
		double toValue = 95.0;
		double num3 = ScreenCenterX();
		double left = base.Left;
		double actualWidth = base.ActualWidth;
		double toValue2 = num3 - num2 / 2.0;
		BeginAnimation(Window.LeftProperty, null);
		BeginAnimation(FrameworkElement.WidthProperty, null);
		BeginAnimation(FrameworkElement.HeightProperty, null);
		BeginAnimation(PillRadiusProperty, null);
		MainPill.BeginAnimation(FrameworkElement.WidthProperty, null);
		BeginAnimation(Window.LeftProperty, new DoubleAnimation(left, toValue2, timeSpan)
		{
			EasingFunction = easingFunction
		});
		BeginAnimation(FrameworkElement.WidthProperty, new DoubleAnimation(actualWidth, num2, timeSpan)
		{
			EasingFunction = easingFunction
		});
		BeginAnimation(FrameworkElement.HeightProperty, new DoubleAnimation(40.0, toValue, timeSpan)
		{
			EasingFunction = easingFunction
		});
		BeginAnimation(PillRadiusProperty, new DoubleAnimation(26.0, timeSpan)
		{
			EasingFunction = easingFunction
		});
		MainPill.BeginAnimation(FrameworkElement.WidthProperty, new DoubleAnimation(MainPill.ActualWidth, num, timeSpan)
		{
			EasingFunction = easingFunction
		});
		DoubleAnimation doubleAnimation = new DoubleAnimation(0.0, 1.0, TimeSpan.FromMilliseconds(280L, 0L))
		{
			EasingFunction = easingFunction
		};
		doubleAnimation.BeginTime = TimeSpan.FromMilliseconds(120L, 0L);
		NotificationText.BeginAnimation(UIElement.OpacityProperty, doubleAnimation);
		_notificationTimer = new DispatcherTimer
		{
			Interval = TimeSpan.FromSeconds(3L)
		};
		_notificationTimer.Tick += delegate
		{
			_notificationTimer?.Stop();
			HideNotificationSmall();
		};
		_notificationTimer.Start();
	}

	private void HideNotificationSmall()
	{
		CubicEase cubicEase = new CubicEase
		{
			EasingMode = EasingMode.EaseOut
		};
		TimeSpan dur = TimeSpan.FromMilliseconds(300L, 0L);
		DoubleAnimation doubleAnimation = new DoubleAnimation(1.0, 0.0, TimeSpan.FromMilliseconds(200L, 0L))
		{
			EasingFunction = cubicEase
		};
		doubleAnimation.Completed += delegate
		{
			NotificationContainer.Visibility = Visibility.Collapsed;
			double num = 220.0;
			double toValue = (_isHovering ? 220.0 : 160.0);
			double num2 = ScreenCenterX();
			double toValue2 = num2 - num / 2.0;
			BeginAnimation(Window.LeftProperty, new DoubleAnimation(base.Left, toValue2, dur)
			{
				EasingFunction = cubicEase
			});
			BeginAnimation(FrameworkElement.WidthProperty, new DoubleAnimation(base.ActualWidth, num, dur)
			{
				EasingFunction = cubicEase
			});
			BeginAnimation(FrameworkElement.HeightProperty, new DoubleAnimation(base.ActualHeight, 40.0, dur)
			{
				EasingFunction = cubicEase
			});
			BeginAnimation(PillRadiusProperty, new DoubleAnimation(PillRadius, 20.0, dur)
			{
				EasingFunction = cubicEase
			});
			MainPill.BeginAnimation(FrameworkElement.WidthProperty, new DoubleAnimation(MainPill.ActualWidth, toValue, dur)
			{
				EasingFunction = cubicEase
			});
			if (_notificationWasProgress && _isShowingProgress)
			{
				ProgressContainer.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0.0, 1.0, TimeSpan.FromMilliseconds(250L, 0L))
				{
					EasingFunction = cubicEase
				});
			}
			else
			{
				TimeText.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0.0, 1.0, TimeSpan.FromMilliseconds(250L, 0L))
				{
					EasingFunction = cubicEase
				});
			}
			_isShowingNotification = false;
		};
		NotificationText.BeginAnimation(UIElement.OpacityProperty, doubleAnimation);
	}

	private void ShowNotificationExpanded()
	{
		CubicEase easingFunction = new CubicEase
		{
			EasingMode = EasingMode.EaseOut
		};
		NotificationContainer.Visibility = Visibility.Visible;
		NotificationContainer.VerticalAlignment = VerticalAlignment.Top;
		NotificationContainer.Margin = new Thickness(0.0, 12.0, 0.0, 0.0);
		NotificationText.Opacity = 0.0;
		NotificationTranslate.Y = -30.0;
		DoubleAnimation animation = new DoubleAnimation(-30.0, 0.0, TimeSpan.FromMilliseconds(300L, 0L))
		{
			EasingFunction = easingFunction
		};
		DoubleAnimation animation2 = new DoubleAnimation(0.0, 1.0, TimeSpan.FromMilliseconds(280L, 0L))
		{
			EasingFunction = easingFunction
		};
		NotificationTranslate.BeginAnimation(TranslateTransform.YProperty, animation);
		NotificationText.BeginAnimation(UIElement.OpacityProperty, animation2);
		_notificationTimer = new DispatcherTimer
		{
			Interval = TimeSpan.FromSeconds(3L)
		};
		_notificationTimer.Tick += delegate
		{
			_notificationTimer?.Stop();
			HideNotificationExpanded();
		};
		_notificationTimer.Start();
	}

	private void HideNotificationExpanded()
	{
		CubicEase easingFunction = new CubicEase
		{
			EasingMode = EasingMode.EaseOut
		};
		DoubleAnimation animation = new DoubleAnimation(0.0, -30.0, TimeSpan.FromMilliseconds(280L, 0L))
		{
			EasingFunction = easingFunction
		};
		DoubleAnimation doubleAnimation = new DoubleAnimation(1.0, 0.0, TimeSpan.FromMilliseconds(250L, 0L))
		{
			EasingFunction = easingFunction
		};
		doubleAnimation.Completed += delegate
		{
			NotificationContainer.Visibility = Visibility.Collapsed;
			NotificationContainer.VerticalAlignment = VerticalAlignment.Center;
			NotificationContainer.Margin = new Thickness(0.0);
			_isShowingNotification = false;
		};
		NotificationTranslate.BeginAnimation(TranslateTransform.YProperty, animation);
		NotificationText.BeginAnimation(UIElement.OpacityProperty, doubleAnimation);
	}

	private void CancelNotification()
	{
		if (_isShowingNotification)
		{
			_notificationTimer?.Stop();
			NotificationContainer.Visibility = Visibility.Collapsed;
			NotificationText.Opacity = 0.0;
			NotificationContainer.VerticalAlignment = VerticalAlignment.Center;
			NotificationContainer.Margin = new Thickness(0.0);
			_isShowingNotification = false;
		}
	}

	private void MainPill_MouseEnter(object sender, MouseEventArgs e)
	{
		if (!_isHovering)
		{
			_isHovering = true;
			if (_state == 0)
			{
				AnimateHover(220.0);
			}
			UpdateHoverState();
		}
	}

	private void MainPill_MouseLeave(object sender, MouseEventArgs e)
	{
		if (_isHovering)
		{
			_isHovering = false;
			if (_state == 0)
			{
				AnimateHover(160.0);
			}
			UpdateHoverState();
		}
	}

	private void AnimateHover(double targetWidth)
	{
		TimeSpan timeSpan = TimeSpan.FromMilliseconds(200L, 0L);
		CubicEase easingFunction = new CubicEase
		{
			EasingMode = EasingMode.EaseOut
		};
		MainPill.BeginAnimation(FrameworkElement.WidthProperty, new DoubleAnimation(targetWidth, timeSpan)
		{
			EasingFunction = easingFunction
		});
	}

	private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
	{
		WindowHelper.ForceActivateWindow(this);
		if (_state == 0)
		{
			_isDragging = true;
			_dragStart = e.GetPosition(this);
			_dragDeltaY = 0.0;
			CaptureMouse();
		}
	}

	private void Window_MouseMove(object sender, MouseEventArgs e)
	{
		if (_isDragging)
		{
			_dragDeltaY = e.GetPosition(this).Y - _dragStart.Y;
		}
	}

	private void Window_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
	{
		if (!_isDragging)
		{
			return;
		}
		bool flag = Math.Abs(_dragDeltaY) < 5.0;
		_isDragging = false;
		ReleaseMouseCapture();
		if (flag)
		{
			if (_state == 0)
			{
				TransitionToExpanded();
			}
			else if (_state == 2)
			{
				TransitionToSmall();
			}
		}
	}

	private void EnableCenterSync()
	{
		if (_centerSyncHandler == null)
		{
			_cachedCenterX = ScreenCenterX();
			_centerSyncHandler = delegate
			{
				base.Left = _cachedCenterX - base.Width / 2.0;
			};
			base.SizeChanged += _centerSyncHandler;
		}
	}

	private void DisableCenterSync()
	{
		if (_centerSyncHandler != null)
		{
			base.SizeChanged -= _centerSyncHandler;
			_centerSyncHandler = null;
		}
	}

	private void TransitionToExpanded()
	{
		if (_isAnimating || _state == 2)
		{
			return;
		}
		CompleteSkeletonLoading();
		CancelNotification();
		_isAnimating = true;
		_state = 2;
		CubicEase cubicEase = new CubicEase
		{
			EasingMode = EasingMode.EaseOut
		};
		DoubleAnimation doubleAnimation = new DoubleAnimation(0.0, TimeSpan.FromMilliseconds(120L, 0L))
		{
			EasingFunction = cubicEase
		};
		doubleAnimation.Completed += delegate
		{
			TimeText.Visibility = Visibility.Collapsed;
			MainPill.BeginAnimation(FrameworkElement.WidthProperty, null);
			MainPill.HorizontalAlignment = HorizontalAlignment.Stretch;
			MainPill.VerticalAlignment = VerticalAlignment.Stretch;
			MainPill.Width = double.NaN;
			MainPill.Height = double.NaN;
			double num = ScreenCenterX();
			double actualWidth = base.ActualWidth;
			double left = base.Left;
			double toValue = num - 450.0;
			BeginAnimation(Window.LeftProperty, null);
			BeginAnimation(FrameworkElement.WidthProperty, null);
			BeginAnimation(Window.LeftProperty, new DoubleAnimation(left, toValue, TimeSpan.FromMilliseconds(300L, 0L))
			{
				EasingFunction = cubicEase
			});
			BeginAnimation(FrameworkElement.WidthProperty, new DoubleAnimation(actualWidth, 900.0, TimeSpan.FromMilliseconds(300L, 0L))
			{
				EasingFunction = cubicEase
			});
			BeginAnimation(PillRadiusProperty, new DoubleAnimation(22.5, TimeSpan.FromMilliseconds(300L, 0L))
			{
				EasingFunction = cubicEase
			});
			DoubleAnimation doubleAnimation2 = new DoubleAnimation(45.0, TimeSpan.FromMilliseconds(300L, 0L))
			{
				EasingFunction = cubicEase
			};
			doubleAnimation2.Completed += delegate
			{
				DoubleAnimation doubleAnimation3 = new DoubleAnimation(500.0, TimeSpan.FromMilliseconds(280L, 0L))
				{
					EasingFunction = cubicEase
				};
				BeginAnimation(PillRadiusProperty, new DoubleAnimation(28.0, TimeSpan.FromMilliseconds(280L, 0L))
				{
					EasingFunction = cubicEase
				});
				doubleAnimation3.Completed += delegate
				{
					EnsureLauncherPanelCreated().PlayEnterAnimation();
					WindowHelper.ForceActivateWindow(this);
					_isAnimating = false;
				};
				BeginAnimation(FrameworkElement.HeightProperty, doubleAnimation3);
			};
			BeginAnimation(FrameworkElement.HeightProperty, doubleAnimation2);
		};
		TimeText.BeginAnimation(UIElement.OpacityProperty, doubleAnimation);
	}

	private void TransitionToSmall()
	{
		if (_isAnimating || _state == 0)
		{
			return;
		}
		CancelNotification();
		_isAnimating = true;
		_isHovering = false;
		WindowHelper.RestoreNoActivate(this);
		bool shouldShowProgress = _isShowingProgress && DownloadManager.IsDownloading;
		HoverTimeText.BeginAnimation(UIElement.OpacityProperty, null);
		HoverPercentText.BeginAnimation(UIElement.OpacityProperty, null);
		HoverTimeText.Opacity = 0.0;
		HoverPercentText.Opacity = 0.0;
		if (shouldShowProgress)
		{
			ClockContainer.Visibility = Visibility.Collapsed;
			ProgressContainer.Visibility = Visibility.Visible;
			TimeTranslate.BeginAnimation(TranslateTransform.XProperty, null);
			TimeTranslate.X = -40.0;
			TimeText.BeginAnimation(UIElement.OpacityProperty, null);
			TimeText.Opacity = 0.0;
			ProgressContainer.Opacity = 1.0;
			ProgressTranslate.BeginAnimation(TranslateTransform.XProperty, null);
			ProgressTranslate.X = 0.0;
			DownloadProgress.Width = 120.0;
		}
		else
		{
			if (_isShowingProgress)
			{
				_isShowingProgress = false;
			}
			ProgressContainer.Visibility = Visibility.Collapsed;
			ClockContainer.Visibility = Visibility.Visible;
			TimeTranslate.BeginAnimation(TranslateTransform.XProperty, null);
			TimeTranslate.X = 0.0;
			TimeText.BeginAnimation(UIElement.OpacityProperty, null);
			TimeText.Opacity = 1.0;
			DownloadProgress.Value = 0.0;
		}
		_launcherPanel?.PlayExitAnimation();
		CubicEase cubicEase = new CubicEase
		{
			EasingMode = EasingMode.EaseOut
		};
		BeginAnimation(PillRadiusProperty, new DoubleAnimation(20.0, TimeSpan.FromMilliseconds(300L, 0L))
		{
			EasingFunction = cubicEase
		});
		DoubleAnimation doubleAnimation = new DoubleAnimation(40.0, TimeSpan.FromMilliseconds(300L, 0L))
		{
			EasingFunction = cubicEase
		};
		doubleAnimation.Completed += delegate
		{
			MainPill.BeginAnimation(FrameworkElement.WidthProperty, null);
			MainPill.HorizontalAlignment = HorizontalAlignment.Center;
			MainPill.VerticalAlignment = VerticalAlignment.Stretch;
			MainPill.Height = 40.0;
			MainPill.Width = MainPill.ActualWidth;
			double actualWidth = base.ActualWidth;
			double left = base.Left;
			double num = ScreenCenterX();
			double toValue = num - 110.0;
			TimeSpan timeSpan = TimeSpan.FromMilliseconds(280L, 0L);
			DoubleAnimation doubleAnimation2 = new DoubleAnimation(actualWidth, 220.0, timeSpan)
			{
				EasingFunction = cubicEase
			};
			DoubleAnimation animation = new DoubleAnimation(left, toValue, timeSpan)
			{
				EasingFunction = cubicEase
			};
			DoubleAnimation animation2 = new DoubleAnimation(MainPill.ActualWidth, 160.0, timeSpan)
			{
				EasingFunction = cubicEase
			};
			doubleAnimation2.Completed += delegate
			{
				MainPill.Width = 160.0;
				if (!shouldShowProgress)
				{
					TimeText.Visibility = Visibility.Visible;
					DoubleAnimation doubleAnimation3 = new DoubleAnimation(0.0, 1.0, TimeSpan.FromMilliseconds(180L, 0L))
					{
						EasingFunction = cubicEase
					};
					doubleAnimation3.Completed += delegate
					{
						_state = 0;
						_isAnimating = false;
					};
					TimeText.BeginAnimation(UIElement.OpacityProperty, doubleAnimation3);
				}
				else
				{
					_state = 0;
					_isAnimating = false;
				}
			};
			BeginAnimation(Window.LeftProperty, animation);
			BeginAnimation(FrameworkElement.WidthProperty, doubleAnimation2);
			MainPill.BeginAnimation(FrameworkElement.WidthProperty, animation2);
		};
		BeginAnimation(FrameworkElement.HeightProperty, doubleAnimation);
	}
}