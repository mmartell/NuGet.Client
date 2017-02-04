﻿using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Diagnostics;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using NuGet.Common;
using NuGet.Credentials;
using NuGet.PackageManagement.UI;
using NuGet.PackageManagement.VisualStudio;
using NuGet.ProjectManagement;

namespace NuGetVSExtension
{
    [Export(typeof(ICredentialServiceProvider))]
    public class DefaultVSCredentialServiceProvider : ICredentialServiceProvider
    {

        private readonly INuGetUILogger _outputConsoleLogger;
        private readonly IServiceProvider _serviceProvider;

        [ImportingConstructor]
        internal DefaultVSCredentialServiceProvider(
           [Import(typeof(SVsServiceProvider))]
            IServiceProvider serviceProvider,
           [Import(typeof(INuGetUILogger))]
           INuGetUILogger outputConsoleLogger
            )
        {
            if (serviceProvider == null)
            {
                throw new ArgumentNullException(nameof(serviceProvider));
            }
            if (outputConsoleLogger == null)
            {
                throw new ArgumentNullException(nameof(outputConsoleLogger));
            }
            _serviceProvider = serviceProvider;
            _outputConsoleLogger = outputConsoleLogger;
        }

        public NuGet.Configuration.ICredentialService GetCredentialService()
        {
            // Initialize the credential providers.
            var credentialProviders = new List<ICredentialProvider>();

            TryAddCredentialProviders(
                credentialProviders,
                Resources.CredentialProviderFailed_VisualStudioAccountProvider,
                () =>
                {
                    var importer = new VsCredentialProviderImporter(
                        _serviceProvider.GetDTE(),
                        VisualStudioAccountProvider.FactoryMethod,
                        (exception, failureMessage) => LogCredentialProviderError(exception, failureMessage));

                    return importer.GetProviders();
                });

            TryAddCredentialProviders(
                credentialProviders,
                Resources.CredentialProviderFailed_VisualStudioCredentialProvider,
                () =>
                {
                    var webProxy = (IVsWebProxy)_serviceProvider.GetService(typeof(SVsWebProxy));

                    Debug.Assert(webProxy != null);

                    return new NuGet.Credentials.ICredentialProvider[] {
                        new VisualStudioCredentialProvider(webProxy)
                    };
                });

            if (PreviewFeatureSettings.DefaultCredentialsAfterCredentialProviders)
            {
                TryAddCredentialProviders(
                credentialProviders,
                Resources.CredentialProviderFailed_DefaultCredentialsCredentialProvider,
                () =>
                {
                    return new NuGet.Credentials.ICredentialProvider[] {
                        new DefaultCredentialsCredentialProvider()
                    };
                });
            }

            // Initialize the credential service.
            var credentialService = new CredentialService(credentialProviders, nonInteractive: false);

            return credentialService;
        }

        private void TryAddCredentialProviders(
            List<NuGet.Credentials.ICredentialProvider> credentialProviders,
            string failureMessage,
            Func<IEnumerable<NuGet.Credentials.ICredentialProvider>> factory)
        {
            try
            {
                var providers = factory();

                if (providers != null)
                {
                    foreach (var credentialProvider in providers)
                    {
                        credentialProviders.Add(credentialProvider);
                    }
                }
            }
            catch (Exception exception)
            {
                LogCredentialProviderError(exception, failureMessage);
            }
        }

        private void LogCredentialProviderError(Exception exception, string failureMessage)
        {
            // Log the user-friendly message to the output console (no stack trace).
            _outputConsoleLogger.Log(
                MessageLevel.Error,
                failureMessage +
                Environment.NewLine +
                ExceptionUtilities.DisplayMessage(exception));

            // Write the stack trace to the activity log.
            ActivityLog.LogWarning(
                ExceptionHelper.LogEntrySource,
                failureMessage +
                Environment.NewLine +
                exception);
        }
    }
}
