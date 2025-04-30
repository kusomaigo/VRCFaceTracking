using System.ComponentModel.DataAnnotations;
using System.Net;
using System.Net.Sockets;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.Logging;
using VRCFaceTracking.Core.Contracts;
using VRCFaceTracking.Core.Contracts.Services;
using VRCFaceTracking.Core.mDNS;
using VRCFaceTracking.Core.Models;
using VRCFaceTracking.Core.Models.ParameterDefinition;
using VRCFaceTracking.Core.OSC;
using VRCFaceTracking.Core.OSC.Query.mDNS;
using VRCFaceTracking.Core.Params;

namespace VRCFaceTracking.Core.Services;

public partial class OscQueryService : ObservableObject
{
    private const string k_chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
    
    // Services
    private readonly ILogger _logger;
    private readonly OscQueryConfigParser _oscQueryConfigParser;
    private readonly MulticastDnsService _multicastDnsService;
    private readonly AvatarConfigParser _configParser;
    private readonly ParameterSenderService _parameterSenderService;
    private static readonly Random Random = new ();
    
    [ObservableProperty] private IAvatarInfo _avatarInfo = new NullAvatarDef("Loading...", "Loading...");
    [ObservableProperty] private List<Parameter> _avatarParameters;

    private readonly IOscTarget _oscTarget;
 
    // Local vars
    private readonly OscRecvService _recvService;
    private readonly ILocalSettingsService _settingsService;
    private readonly HttpHandler _httpHandler;

    private bool _alreadyUp;

    public OscQueryService(
        ILogger<OscQueryService> logger,
        OscQueryConfigParser oscQueryConfigParser,
        AvatarConfigParser avatarConfigParser,
        ParameterSenderService parameterSenderService,
        IOscTarget oscTarget,
        MulticastDnsService multicastDnsService,
        OscRecvService recvService,
        ILocalSettingsService settingsService,
        HttpHandler httpHandler
    )
    {
        _oscQueryConfigParser = oscQueryConfigParser;
        _configParser = avatarConfigParser;
        _parameterSenderService = parameterSenderService;
        _logger = logger;
        _recvService = recvService;
        _recvService.OnMessageReceived = HandleNewMessage;
        _multicastDnsService = multicastDnsService;
        _oscTarget = oscTarget;
        _settingsService = settingsService;
        _httpHandler = httpHandler;
        _alreadyUp = false;

        _oscTarget.PropertyChanged += async (_, args) =>
        {
            if (!_alreadyUp)
            {
                return;
            }
            if (args.PropertyName is not nameof(IOscTarget.UseOscQuery))
            {
                return;
            }

            if (args.PropertyName is nameof(IOscTarget.UseOscQuery))
            {
                _logger.LogInformation($"WOW OSC QUERY TOGGLED TO {_oscTarget.UseOscQuery}");
            }

            await InitializeAsync();

        };
    }

    public async Task InitializeAsync()
    {
        if (!_alreadyUp)
        {
            _logger.LogDebug("OSC Service Initializing");
            await _settingsService.Load(_oscTarget);
            //_logger.LogInformation($"{_oscTarget.DestinationAddress}");
        } else
        {
            _logger.LogDebug("OSC Service Re-Initializing");
        }
        //// clear oscTarget.isConnected
        _oscTarget.IsConnected = false;

        _logger.LogInformation($"UseOscQuery status: {_oscTarget.UseOscQuery}");

        // this is kinda pointless... 
        //(bool listenerSuccess, bool senderSuccess) result = (false, false);

        if (string.IsNullOrWhiteSpace(_oscTarget.DestinationAddress))
        {
            return;  // Return both false as we cant bind to anything without an address
        }

        // first attempt to just hijack entirely of osc query service in name
        if (_oscTarget.UseOscQuery)
        {
            _logger.LogInformation("Using OSCQuery Service");
            _oscTarget.VisualInPort = 0;
            _multicastDnsService.OnVrcClientDiscovered += FirstClientDiscovered;
            _multicastDnsService.SendQuery("_oscjson._tcp.local");
        } else
        {
            _logger.LogInformation("Not using OSCQuery Service");
            // listener
            // bind to port in setting
            IPEndPoint recvEndPoint = _recvService.UpdateTarget(new IPEndPoint(IPAddress.Parse(_oscTarget.DestinationAddress), _oscTarget.InPort));
            //result.listenerSuccess = (recvEndPoint != null);
            // sender
            //result.senderSuccess = _parameterSenderService.sendServiceConnected();
        }
        _alreadyUp = true;

        //_logger.LogDebug("OSC Service Initialized with result {0}", result);
        //_logger.LogDebug("OSC Sender/Receiver Initialized with result {0}", result);
        await Task.CompletedTask;
    }
    
    private void FirstClientDiscovered()
    {
        _multicastDnsService.OnVrcClientDiscovered -= FirstClientDiscovered;
        
        _logger.LogInformation("OSCQuery detected. Setting port negotiation to autopilot.");
        
        var randomStr = new string(Enumerable.Repeat(k_chars, 6).Select(s => s[Random.Next(s.Length)]).ToArray());
        _httpHandler.OnHostInfoQueried += HandleNewAvatarWrapper;

        var listener = new TcpListener(IPAddress.Any, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();

        var recvEndpoint = _recvService.UpdateTarget(new IPEndPoint(IPAddress.Parse(_oscTarget.DestinationAddress), port));
        if (recvEndpoint == null)
        {
            _logger.LogError("Very strange. We were unable to bind to a random port.");
            // pretend not using OSCQuery and bind to default OSC IN port
            recvEndpoint = new IPEndPoint(IPAddress.Parse(_oscTarget.DestinationAddress), _oscTarget.VisualInPort);
        }

        // TODO: Move this somewhere more appropriate
        //var listener = new TcpListener(IPAddress.Any, 0);
        //listener.Start();
        //var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        //listener.Stop();
        _httpHandler.SetAppName("VRCFT-" + randomStr);
        _httpHandler.BindTo($"http://127.0.0.1:{recvEndpoint.Port}/", recvEndpoint.Port);
        //_httpHandler.BindTo($"http://127.0.0.1:{port}/", recvEndpoint.Port);

        //// update OSCTarget Visual In port
        _oscTarget.VisualInPort = recvEndpoint.Port;

        // Advertise our OSC JSON and OSC endpoints (OSC JSON to display the silly lil popup in-game)
        _logger.LogInformation($"port {port}");
        _multicastDnsService.Advertise("_oscjson._tcp", "VRCFT-"+randomStr, port, IPAddress.Loopback);
        _multicastDnsService.Advertise("_osc._udp", "VRCFT-"+randomStr, recvEndpoint.Port, IPAddress.Loopback);
        
        HandleNewAvatar();
    }

    private async void HandleNewAvatar(string newId = null)
    {
        var newAvatar = default((IAvatarInfo avatarInfo, List<Parameter> relevantParameters)?);
        if (newId == null)
        {
            _httpHandler.OnHostInfoQueried -= HandleNewAvatarWrapper;
            newAvatar = await _oscQueryConfigParser.ParseAvatar("");
        }
        else
        {
            // handle normal osc
            newAvatar = await _configParser.ParseAvatar(newId);
        }
        if (newAvatar.HasValue)
        {
            AvatarInfo = newAvatar.Value.avatarInfo;
            AvatarParameters = newAvatar.Value.relevantParameters;
        }
    }
        
    private void HandleNewAvatarWrapper()
    {
        HandleNewAvatar();
    }

    private void HandleNewMessage(OscMessage msg)
    {
        switch (msg.Address)
        {
            case "/avatar/change":
                HandleNewAvatar(msg.Value as string);
                break;
            case "/vrcft/settings/forceRelevant":   // Endpoint for external tools to force vrcft to send all parameters
                if (msg.Value is bool relevancy)
                {
                    _parameterSenderService.AllParametersRelevant = relevancy;
                }

                break;
            /*
            case "/avatar/parameters/EyeTrackingActive":
                if (UnifiedLibManager.EyeStatus != ModuleState.Uninitialized)
                {
                    if (!msg.Value.BoolValue)
                        UnifiedLibManager.EyeStatus = ModuleState.Idle;
                    else UnifiedLibManager.EyeStatus = ModuleState.Active;
                }
                break;
            case "/avatar/parameters/LipTrackingActive":
            case "/avatar/parameters/ExpressionTrackingActive":
                {
                    if (!msg.Value.BoolValue)
                if (UnifiedLibManager.ExpressionStatus != ModuleState.Uninitialized)
                        UnifiedLibManager.ExpressionStatus = ModuleState.Idle;
                    else UnifiedLibManager.ExpressionStatus = ModuleState.Active;
                }
                break;
            */
        }
    }

    partial void OnAvatarParametersChanged(List<Parameter> value)
    {
        _logger.LogDebug("Configuration loaded. Checking for native tracking parameters...");
        var hasLoadedNative = value.Any(p => p.GetParamNames().Any(t => t.paramName.StartsWith("/tracking/")));
        if (hasLoadedNative)
        {
            _logger.LogWarning("Native tracking parameters detected.");
        }

        var deprecatedParams = value.Where(p => p.Deprecated).ToList();

        _logger.LogInformation(value.Count + " parameters loaded.");
        if (deprecatedParams.Any())
        {
            _logger.LogWarning(
                deprecatedParams.Count +
                " Legacy parameters detected. " +
                "Please consider updating the avatar to use the latest documented parameters.");
            _logger.LogDebug($"Loaded deprecated parameters: {string.Join(", ", deprecatedParams.SelectMany(x => x.GetParamNames()).Select(y => y.paramName))}");
        }
    }
}