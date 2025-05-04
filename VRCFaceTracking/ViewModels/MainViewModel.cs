using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using VRCFaceTracking.Contracts.ViewModels;
using VRCFaceTracking.Core;
using VRCFaceTracking.Core.Contracts;
using VRCFaceTracking.Core.Contracts.Services;
using VRCFaceTracking.Core.OSC;
using VRCFaceTracking.Core.Services;

namespace VRCFaceTracking.ViewModels;

public partial class MainViewModel : ObservableRecipient, INavigationAware
{
    public ILibManager LibManager { get; }
    public OscQueryService ParameterOutputService { get; }
    public OscRecvService OscRecvService { get; }
    public OscSendService OscSendService { get; }
    public IOscTarget OscTarget { get; }

    private int _messagesRecvd;
    [ObservableProperty] private int _messagesInPerSec;

    private int _messagesSent;
    [ObservableProperty] private int _messagesOutPerSec;

    [ObservableProperty] private bool _noModulesInstalled;
    
    [ObservableProperty] private bool _oscWasDisabled;

    private readonly DispatcherTimer msgCounterTimer;

    public MainViewModel(
        ILibManager libManager,
        OscQueryService parameterOutputService,
        IModuleDataService moduleDataService,
        IOscTarget oscTarget,
        OscRecvService oscRecvService,
        OscSendService oscSendService
        )
    {
        //Services
        LibManager = libManager;
        ParameterOutputService = parameterOutputService;
        OscTarget = oscTarget;
        OscRecvService = oscRecvService;
        OscSendService = oscSendService;
        
        // Modules
        var installedNewModules = moduleDataService.GetInstalledModules();
        var installedLegacyModules = moduleDataService.GetLegacyModules().Count();
        NoModulesInstalled = !installedNewModules.Any() && installedLegacyModules == 0;

        // Message Timer
        msgCounterTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        msgCounterTimer.Start();
    }

    private void MessageReceived(OscMessage msg) => _messagesRecvd++;
    private void MessageDispatched(int msgCount) => _messagesSent += msgCount;

    private void TimerTick(object? sender, object e)
    {
        MessagesInPerSec = _messagesRecvd;
        _messagesRecvd = 0;

        MessagesOutPerSec = _messagesSent;
        _messagesSent = 0;

        foreach (var mld in LibManager.LoadedModulesLiveData)
        {
            //var rnd = new Random();
            mld.ModuleUpdateInfo.GetLatestUpdateRate(); // = rnd.Next(1, 100);

        }
    }

    public void OnNavigatedFrom()
    {
        OscRecvService.OnMessageReceived -= MessageReceived;
        OscSendService.OnMessagesDispatched -= MessageDispatched;
        msgCounterTimer.Tick -= TimerTick;
    }

    public void OnNavigatedTo(object parameter)
    {
        OscRecvService.OnMessageReceived += MessageReceived;
        OscSendService.OnMessagesDispatched += MessageDispatched;
        msgCounterTimer.Tick += TimerTick;
    }

    ~MainViewModel()
    {
        OscRecvService.OnMessageReceived -= MessageReceived;
        OscSendService.OnMessagesDispatched -= MessageDispatched;
        try
        {
            if (msgCounterTimer != null && msgCounterTimer.IsEnabled) msgCounterTimer.Stop();
        } catch
        {
            // it's already disposed so... 
        }
    }
}
