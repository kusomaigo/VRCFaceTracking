using System.ComponentModel.DataAnnotations;
using System.Net;
using System.Net.Sockets;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using VRCFaceTracking.Core.Contracts;
using VRCFaceTracking.Core.Contracts.Services;
using VRCFaceTracking.Core.OSC;

namespace VRCFaceTracking.Core.Services;

public class OscRecvService : BackgroundService
{
    private readonly ILogger<OscRecvService> _logger;
    private readonly IOscTarget _oscTarget;
    private readonly ILocalSettingsService _settingsService;
    
    private Socket _recvSocket;
    private readonly byte[] _recvBuffer = new byte[4096];
    
    private CancellationTokenSource _cts, _linkedToken;
    private CancellationToken _stoppingToken;
    
    public Action<OscMessage> OnMessageReceived = _ => { };

    public OscRecvService(
        ILogger<OscRecvService> logger,
        IOscTarget oscTarget,
        ILocalSettingsService settingsService
    )
    {
        _logger = logger;
        _cts = new CancellationTokenSource();

        _oscTarget = oscTarget;
        _settingsService = settingsService;
        
        _oscTarget.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName is not nameof(IOscTarget.InPort))
            {
                return;
            }

            //if (args.PropertyName is nameof(IOscTarget.UseOscQuery))
            //{
            //    _logger.LogInformation($"WOW OSC QUERY TOGGLED TO {_oscTarget.UseOscQuery}");
            //}

            if (_oscTarget.InPort == default || _oscTarget.UseOscQuery == true)
            {
                return;
            }

            var validationResults = new List<ValidationResult>();
            var context = new ValidationContext(oscTarget);

            //// no point in validating the whole thing right? 
           
            //if (!Validator.TryValidateObject(oscTarget, context, validationResults, true))
            //{
            //    var errorMessages = string.Join(Environment.NewLine, validationResults.Select(vr => vr.ErrorMessage));
            //    _logger.LogInformation($"DestinationAddress: {_oscTarget.DestinationAddress}");
            //    _logger.LogWarning($"{errorMessages} Reverting to default.");
            //    _oscTarget.DestinationAddress = "127.0.0.1";
            //    _oscTarget.InPort = 9001; //// how to not hardcode here????
            //}

            //UpdateTarget(new IPEndPoint(IPAddress.Parse(_oscTarget.DestinationAddress), _oscTarget.InPort));

            //// shouldn't the receive endpoint always be localhost? 
            UpdateTarget(new IPEndPoint(IPAddress.Parse("127.0.0.1"), _oscTarget.InPort));
            //_oscTarget.VisualInPort = _oscTarget.InPort;
        };
    }

    public async override Task StartAsync(CancellationToken cancellationToken)
    {
        //await _settingsService.Load(_oscTarget);

        await base.StartAsync(cancellationToken);
    }

    public IPEndPoint UpdateTarget(IPEndPoint endpoint)
    {
        _cts.Cancel();
        _recvSocket?.Close();
        _oscTarget.IsConnected = false;

        _recvSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
        
        try
        {
            _recvSocket.Bind(endpoint);
            _oscTarget.IsConnected = true;

            ////// also update settings from the back
            //_oscTarget.InPort = endpoint.Port;

            //// log success of UpdateTarget
            _logger.LogInformation($"OSC Receive Endpoint successfully updated to {endpoint}");
            _oscTarget.VisualInPort = endpoint.Port;
            return (IPEndPoint)_recvSocket.LocalEndPoint;
        }
        catch (SocketException ex)
        {
            _logger.LogWarning($"OSC Receive Endpoint failed to bind to {endpoint}. {ex.Message}");
        }
        finally
        {
            _cts = new CancellationTokenSource();
            _linkedToken = CancellationTokenSource.CreateLinkedTokenSource(_stoppingToken, _cts.Token);
        }
        return null;
    }
    
    protected async override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _stoppingToken = stoppingToken;
        
        _linkedToken = CancellationTokenSource.CreateLinkedTokenSource(_stoppingToken, _cts.Token);
        
        while (!_stoppingToken.IsCancellationRequested)
        {
            if (_linkedToken.IsCancellationRequested || _recvSocket is not { IsBound: true } || !_oscTarget.IsConnected)
            {
                //// wait so this loop doesn't murder the whole program for some reason
                await Task.Delay(200);
                continue;
            }

            try
            {
                if (_recvSocket.Poll(1000, SelectMode.SelectRead))
                {
                    //var bytesReceived = await _recvSocket.ReceiveAsync(_recvBuffer, _linkedToken.Token);
                    var bytesReceived = _recvSocket.Receive(_recvBuffer, SocketFlags.None);
                    var offset = 0;
                    //var newMsg = await Task.Run(() => OscMessage.TryParseOsc(_recvBuffer, bytesReceived, ref offset), stoppingToken);
                    OscMessage newMsg = OscMessage.TryParseOsc(_recvBuffer, bytesReceived, ref offset);
                    if (newMsg == null)
                    {
                        continue;
                    }

                    OnMessageReceived(newMsg);
                }
                else
                {
                     await Task.Delay(1, _stoppingToken);
                }

            }
            catch (Exception e)
            {
                // We don't care about operation cancellations as they're intentional and carefully controlled
                if (e.GetType() == typeof(OperationCanceledException))
                {
                    continue;
                }
                
                _logger.LogError("Error encountered in OSC Receive thread: {e}", e);
                SentrySdk.CaptureException(e, scope => scope.SetExtra("recvBuffer", _recvBuffer));
            }
        }
    }
    public bool targetConnected()
    {
        return _oscTarget.IsConnected;
    }
}