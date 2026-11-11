// Copyright (c) Zenix. All rights reserved.
// Licensed under the proprietary and confidential license.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using FluentAssertions;
using Xunit;
using Zenix.Core.Zalo.Contracts;

namespace Zenix.Tests.Zalo.Contracts;

/// <summary>
/// Architecture and reflection verification tests for Zenix Zalo service interface contracts.
/// Asserts strict Clean MVVM separation, absence of UI types, and mandatory CancellationToken patterns.
/// </summary>
public sealed class ContractsArchitectureTests
{
    private static readonly Assembly TargetAssembly = typeof(IZaloClient).Assembly;
    private const string TargetNamespace = "Zenix.Core.Zalo.Contracts";

    private static readonly Type[] RequiredContractInterfaces =
    [
        typeof(IZaloClient),
        typeof(IZaloAuthService),
        typeof(IZaloGatewayService),
        typeof(IZaloCryptoService),
        typeof(IZaloMessageService),
        typeof(IZaloMediaService),
        typeof(IZaloContactService),
        typeof(IZaloStorageService),
        typeof(IConversationRepository),
        typeof(IMessageRepository),
        typeof(IContactRepository),
        typeof(IFilePickerService),
        typeof(IDialogService),
        typeof(IChatViewModel),
        typeof(IConversationListViewModel)
    ];

    /// <summary>
    /// Verifies that all required service, repository, and ViewModel contracts are declared as public interfaces.
    /// </summary>
    [Fact]
    [Trait("Category", "Contracts")]
    public void AllRequiredContracts_ExistAndArePublicInterfaces()
    {
        foreach (var contractType in RequiredContractInterfaces)
        {
            contractType.Should().NotBeNull();
            contractType.IsInterface.Should().BeTrue($"{contractType.Name} must be an interface.");
            contractType.IsPublic.Should().BeTrue($"{contractType.Name} must be public.");
            contractType.Namespace.Should().Be(TargetNamespace, $"{contractType.Name} must reside in {TargetNamespace}.");
        }
    }

    /// <summary>
    /// Verifies that all interface names in the contracts namespace start with 'I'.
    /// </summary>
    [Fact]
    [Trait("Category", "Contracts")]
    public void AllInterfaces_FollowStandardNamingConvention()
    {
        var interfaces = TargetAssembly.GetTypes()
            .Where(t => t.Namespace == TargetNamespace && t.IsInterface);

        foreach (var iface in interfaces)
        {
            iface.Name.Should().StartWith("I", $"{iface.Name} is an interface and must start with 'I'.");
        }
    }

    /// <summary>
    /// Verifies that zero types in the Contracts namespace reference or depend on UI visual elements.
    /// </summary>
    [Fact]
    [Trait("Category", "Contracts")]
    public void ContractsNamespace_ContainsZeroReferencesToUiVisualTypes()
    {
        var prohibitedNamespacePrefixes = new[] { "Microsoft." + "UI.Xaml", "Windows." + "UI.Xaml" };
        var prohibitedTypeNames = new[] { "UI" + "Element", "Framework" + "Element", "Window", "Content" + "Dialog" };

        var types = TargetAssembly.GetTypes()
            .Where(t => t.Namespace == TargetNamespace)
            .ToList();

        types.Should().NotBeEmpty("Contracts namespace must contain types to inspect.");

        foreach (var type in types)
        {
            void AssertNotProhibited(Type? t, string memberContext)
            {
                if (t == null)
                {
                    return;
                }

                if (t.IsGenericType)
                {
                    foreach (var arg in t.GetGenericArguments())
                    {
                        AssertNotProhibited(arg, memberContext);
                    }
                }

                var fullNamespace = t.Namespace ?? string.Empty;
                foreach (var prefix in prohibitedNamespacePrefixes)
                {
                    fullNamespace.StartsWith(prefix, StringComparison.Ordinal)
                        .Should().BeFalse($"Type {type.Name} ({memberContext}) references prohibited UI namespace {prefix}");
                }

                foreach (var prohibitedName in prohibitedTypeNames)
                {
                    t.Name.Should().NotBe(prohibitedName, $"Type {type.Name} ({memberContext}) references prohibited UI type {prohibitedName}");
                }
            }

            AssertNotProhibited(type.BaseType, "BaseType");

            foreach (var iface in type.GetInterfaces())
            {
                AssertNotProhibited(iface, "Interface");
            }

            foreach (var prop in type.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static))
            {
                AssertNotProhibited(prop.PropertyType, $"Property {prop.Name}");
            }

            foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static))
            {
                AssertNotProhibited(method.ReturnType, $"Method {method.Name} return type");
                foreach (var param in method.GetParameters())
                {
                    AssertNotProhibited(param.ParameterType, $"Method {method.Name} parameter {param.Name}");
                }
            }

            foreach (var evt in type.GetEvents(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static))
            {
                AssertNotProhibited(evt.EventHandlerType, $"Event {evt.Name}");
            }
        }
    }

    /// <summary>
    /// Verifies that 100% of asynchronous methods on contract interfaces take a CancellationToken with a default value.
    /// </summary>
    [Fact]
    [Trait("Category", "Contracts")]
    public void AllAsynchronousMethodsInContracts_MustAcceptCancellationToken_WithDefaultValue()
    {
        var interfaces = TargetAssembly.GetTypes()
            .Where(t => t.Namespace == TargetNamespace && t.IsInterface)
            .ToList();

        var asyncMethodCount = 0;

        foreach (var iface in interfaces)
        {
            var methods = iface.GetMethods();

            foreach (var method in methods)
            {
                var returnType = method.ReturnType;
                var isAsync = returnType == typeof(Task) ||
                              (returnType.IsGenericType && returnType.GetGenericTypeDefinition() == typeof(Task<>)) ||
                              returnType == typeof(ValueTask) ||
                              (returnType.IsGenericType && returnType.GetGenericTypeDefinition() == typeof(ValueTask<>)) ||
                              (returnType.IsGenericType && returnType.GetGenericTypeDefinition() == typeof(IAsyncEnumerable<>));

                if (!isAsync)
                {
                    continue;
                }

                asyncMethodCount++;

                var parameters = method.GetParameters();
                var ctParam = parameters.FirstOrDefault(p => p.ParameterType == typeof(CancellationToken));

                ctParam.Should().NotBeNull(
                    $"Interface {iface.Name} method {method.Name} is asynchronous but does not accept a CancellationToken.");

                ctParam!.HasDefaultValue.Should().BeTrue(
                    $"Interface {iface.Name} method {method.Name} CancellationToken parameter must have default value (cancellationToken = default).");
            }
        }

        asyncMethodCount.Should().BeGreaterThan(20, "Should inspect and verify a comprehensive set of async contract methods.");
    }

    /// <summary>
    /// Verifies that ViewModel contracts adhere to Clean MVVM patterns: INotifyPropertyChanged, ICommand, and ObservableCollection.
    /// </summary>
    [Fact]
    [Trait("Category", "Contracts")]
    public void ViewModelContracts_AdhereToCleanMvvmConventions()
    {
        var viewModelTypes = new[] { typeof(IChatViewModel), typeof(IConversationListViewModel) };

        foreach (var vmType in viewModelTypes)
        {
            typeof(INotifyPropertyChanged).IsAssignableFrom(vmType)
                .Should().BeTrue($"{vmType.Name} must implement INotifyPropertyChanged.");

            var commandProperties = vmType.GetProperties()
                .Where(p => p.Name.EndsWith("Command", StringComparison.Ordinal));

            foreach (var cmdProp in commandProperties)
            {
                typeof(ICommand).IsAssignableFrom(cmdProp.PropertyType)
                    .Should().BeTrue($"Property {vmType.Name}.{cmdProp.Name} must implement ICommand.");
            }
        }

        // Specific collection checks
        var chatMessagesProp = typeof(IChatViewModel).GetProperty("Messages");
        chatMessagesProp.Should().NotBeNull();
        chatMessagesProp!.PropertyType.GetGenericTypeDefinition().Should().Be(typeof(ObservableCollection<>));

        var conversationListProp = typeof(IConversationListViewModel).GetProperty("Conversations");
        conversationListProp.Should().NotBeNull();
        conversationListProp!.PropertyType.GetGenericTypeDefinition().Should().Be(typeof(ObservableCollection<>));
    }

    /// <summary>
    /// Verifies that services managing persistent connections implement IAsyncDisposable.
    /// </summary>
    [Fact]
    [Trait("Category", "Contracts")]
    public void AsyncDisposableServices_ImplementIAsyncDisposable()
    {
        var disposableServices = new[]
        {
            typeof(IZaloClient),
            typeof(IZaloGatewayService),
            typeof(IZaloStorageService)
        };

        foreach (var svc in disposableServices)
        {
            typeof(IAsyncDisposable).IsAssignableFrom(svc)
                .Should().BeTrue($"{svc.Name} must implement IAsyncDisposable for graceful resource teardown.");
        }
    }

    /// <summary>
    /// Verifies that auxiliary enums and event records are declared with appropriate types and members.
    /// </summary>
    [Fact]
    [Trait("Category", "Contracts")]
    public void AuxiliaryEnumsAndRecords_AreProperlyDeclared()
    {
        typeof(QrLoginStep).IsEnum.Should().BeTrue();
        typeof(ConfirmationResult).IsEnum.Should().BeTrue();
        typeof(NotificationSeverity).IsEnum.Should().BeTrue();

        var recordTypes = new[]
        {
            typeof(QrCodeState),
            typeof(QrConfirmedUser),
            typeof(QrLoginStepResult),
            typeof(ZaloIncomingFrameEventArgs),
            typeof(ZaloConnectionStateChangedEventArgs),
            typeof(MessageReceivedEventArgs),
            typeof(MessageStatusChangedEventArgs),
            typeof(MessageRecalledEventArgs),
            typeof(MediaTransferProgress)
        };

        foreach (var rec in recordTypes)
        {
            rec.IsClass.Should().BeTrue($"{rec.Name} must be a class/record.");
            rec.IsPublic.Should().BeTrue($"{rec.Name} must be public.");
            rec.GetProperties().Should().NotBeEmpty($"{rec.Name} must have properties.");
        }
    }
}
