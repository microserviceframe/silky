using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using Nacos.V2;
using Nacos.V2.Naming.Dtos;
using Silky.Core.Rpc;
using Silky.RegistryCenter.Nacos.Configuration;
using Silky.RegistryCenter.Nacos.Listeners;
using Silky.Rpc.Endpoint;
using Silky.Rpc.Runtime.Server;

namespace Silky.RegistryCenter.Nacos
{
    public class NacosServerRegister : ServerRegisterBase
    {
        private readonly INacosNamingService _nacosNamingService;
        private readonly NacosRegistryCenterOptions _nacosRegistryCenterOptions;
        private readonly IServiceProvider _serviceProvider;
        private readonly IServerRegisterProvider _serverRegisterProvider;

        private ConcurrentDictionary<string, ServerListener> m_serverListeners = new();


        public NacosServerRegister(IServerManager serverManager,
            IServerProvider serverProvider,
            IServiceProvider serviceProvider,
            INacosNamingService nacosNamingService,
            IOptionsMonitor<NacosRegistryCenterOptions> nacosRegistryCenterOptions,
            IServerRegisterProvider serverRegisterProvider)
            : base(serverManager,
                serverProvider)
        {
            _nacosNamingService = nacosNamingService;
            _serverRegisterProvider = serverRegisterProvider;
            _serviceProvider = serviceProvider;
            _nacosRegistryCenterOptions = nacosRegistryCenterOptions.CurrentValue;
        }

        public async override Task RemoveSelfServer()
        {
        }

        protected override async Task RemoveRpcEndpoint(string hostName, IRpcEndpoint rpcEndpoint)
        {
        }

        protected override async Task CacheServers()
        {
            var serverNames = await _serverRegisterProvider.GetAllServerNames();
            foreach (var serverName in serverNames)
            {
                await CreateServerListener(serverName);
            }
        }

        internal async Task CreateServerListener(string serverName)
        {
            if (!m_serverListeners.ContainsKey(serverName))
            {
                var serverListener = new ServerListener(this);
                m_serverListeners.TryAdd(serverName, serverListener);
                await _nacosNamingService.Subscribe(serverName, _nacosRegistryCenterOptions.GroupName, serverListener);
                var serverInstances = await _nacosNamingService.GetAllInstances(serverName);
                await UpdateServer(serverName, serverInstances);
            }
        }

        internal async Task UpdateServer(string serverName, List<Instance> instances)
        {
            var services = await _serviceProvider.GetServices(serverName);
            var serverDescriptor =
                _serverRegisterProvider.GetServerDescriptor(serverName, instances, services);
            _serverManager.Update(serverDescriptor);
        }

        protected override async Task RegisterServerToServiceCenter(ServerDescriptor serverDescriptor)
        {
            await _serviceProvider.PublishServices(serverDescriptor.HostName, serverDescriptor.Services);
            foreach (var endpoint in serverDescriptor.Endpoints)
            {
                if (endpoint.ServiceProtocol == ServiceProtocol.Ws)
                {
                    continue;
                }

                var instance = new Instance()
                {
                    InstanceId = endpoint.ToString(),
                    ServiceName = serverDescriptor.HostName,
                    Ephemeral = true,
                    Enabled = true,
                    Healthy = true,
                    Ip = endpoint.Host,
                    Port = endpoint.Port,
                    Metadata = new Dictionary<string, string>()
                    {
                        { "ServiceProtocol", endpoint.ServiceProtocol.ToString() },
                        { "TimeStamp", serverDescriptor.TimeStamp.ToString() },
                        { "ProcessorTime", endpoint.ProcessorTime.ToString() },
                        {
                            "SupportWebsocket",
                            serverDescriptor.Endpoints.Any(p => p.ServiceProtocol == ServiceProtocol.Ws).ToString()
                        }
                    }
                };
                await _nacosNamingService.RegisterInstance(
                    serverDescriptor.HostName,
                    _nacosRegistryCenterOptions.GroupName,
                    instance);
                await _serverRegisterProvider.AddServer();
            }
        }


        protected override async Task RemoveServiceCenterExceptRpcEndpoint(IServer server)
        {
        }
    }
}