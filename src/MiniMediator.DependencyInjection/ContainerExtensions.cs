﻿using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using MiniMediator;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Net.WebSockets;

namespace Microsoft.Extensions.DependencyInjection
{

    public static partial class ContainerExtensions
    {
        public static IServiceCollection AddMediator(this IServiceCollection services)
        {
            return AddMediator(services, options => { });
        }

        public static IServiceCollection AddMediator(this IServiceCollection services, Action<MediatorOptions> options)
        {
            var optionsInstance = new MediatorOptions();
            options(optionsInstance);

            RegisterAssembly(services, optionsInstance);

            var handlerTypes = services
                .SelectMany(descriptor => descriptor.ServiceType.GetInterfaces().Select(iface => (type: descriptor.ServiceType, iface)))
                .Where(serviceType => serviceType.iface.IsGenericType && serviceType.iface.GetGenericTypeDefinition() == typeof(IMessageHandler<>))
                .Select(serviceType => (serviceType.type, messageType: serviceType.iface.GetGenericArguments().Single()))
                .ToArray();

            services.Add(
                new ServiceDescriptor(
                    typeof(Mediator),
                    provider => {
                        var mediator = new ContainerMediator(
                            provider,
                            handlerTypes,
                            provider.GetService<ILogger<Mediator>>(),
                            optionsInstance.LoggingLevel
                        );

                        return mediator;
                    },
                    optionsInstance.Lifetime
                )
            );

            return services;
        }

        private static void RegisterAssembly(IServiceCollection services, MediatorOptions options)
        {
            var handlerTypes = options.Assemblies
                .SelectMany(assembly => assembly.GetTypes())
                .Where(type => !type.IsAbstract && type
                    .GetInterfaces()
                    .Any(iface => iface.IsGenericType && iface.GetGenericTypeDefinition() == typeof(IMessageHandler<>))
                )
                .ToArray();


            foreach (var handlerType in handlerTypes)
            {
                services.TryAdd(new ServiceDescriptor(handlerType, handlerType, options.HandlerLifetime));
            }
        }
    }
}
