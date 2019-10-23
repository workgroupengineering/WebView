﻿using System;
using Xilium.CefGlue;
using Xilium.CefGlue.Common.Handlers;

namespace WebViewControl {

    partial class WebView {

        private class InternalRequestHandler : RequestHandler, IDisposable {

            private WebView OwnerWebView { get; }
            
            private InternalResourceRequestHandler ResourceRequestHandler { get; }

            public InternalRequestHandler(WebView webView) {
                OwnerWebView = webView;
                ResourceRequestHandler = new InternalResourceRequestHandler(OwnerWebView);
            }

            public void Dispose() {
                ResourceRequestHandler.Dispose();
            }

            protected override bool OnQuotaRequest(CefBrowser browser, string originUrl, long newSize, CefRequestCallback callback) {
                using (callback) {
                    callback.Continue(true);
                    return true;
                }
            }

            protected override bool GetAuthCredentials(CefBrowser browser, string originUrl, bool isProxy, string host, int port, string realm, string scheme, CefAuthCallback callback) {
                using (callback) {
                    if (OwnerWebView.ProxyAuthentication != null) {
                        callback.Continue(OwnerWebView.ProxyAuthentication.UserName, OwnerWebView.ProxyAuthentication.Password);
                    }
                    return true;
                }
            }

            protected override bool OnBeforeBrowse(CefBrowser browser, CefFrame frame, CefRequest request, bool userGesture, bool isRedirect) {
                if (UrlHelper.IsInternalUrl(request.Url)) {
                    return false;
                }
                
                if (OwnerWebView.IsHistoryDisabled && request.TransitionType.HasFlag(CefTransitionType.ForwardBackFlag)) {
                    return true;
                }
               
                var cancel = false;
                var beforeNavigate = OwnerWebView.BeforeNavigate;
                if (beforeNavigate != null) {
                    var wrappedRequest = new Request(request, OwnerWebView.GetRequestUrl(request.Url, (ResourceType) request.ResourceType));
                    OwnerWebView.ExecuteWithAsyncErrorHandling(() => beforeNavigate(wrappedRequest));
                    cancel = wrappedRequest.Canceled;
                }

                return cancel;
            }

            protected override bool OnCertificateError(CefBrowser browser, CefErrorCode certError, string requestUrl, CefSslInfo sslInfo, CefRequestCallback callback) {
                using (callback) {
                    if (OwnerWebView.IgnoreCertificateErrors) {
                        callback.Continue(true);
                        return true;
                    }
                    return false;
                }
            }

            protected override void OnRenderProcessTerminated(CefBrowser browser, CefTerminationStatus status) {
                OwnerWebView.HandleRenderProcessCrashed();

                const string ExceptionPrefix = "WebView render process ";

                Exception exception;

                switch (status) {
                    case CefTerminationStatus.ProcessCrashed:
                        exception = new RenderProcessCrashedException(ExceptionPrefix + "crashed");
                        break;
                    case CefTerminationStatus.WasKilled:
                        exception = new RenderProcessKilledException(ExceptionPrefix + "was killed");
                        break;
                    case CefTerminationStatus.OutOfMemory:
                        exception = new RenderProcessKilledException(ExceptionPrefix + "ran out of memory");
                        break;
                    default:
                        exception = new RenderProcessCrashedException(ExceptionPrefix + "terminated with an unknown reason");
                        break;
                }

                OwnerWebView.ExecuteWithAsyncErrorHandling(() => throw exception);
            }

            protected override CefResourceRequestHandler GetResourceRequestHandler(CefBrowser browser, CefFrame frame, CefRequest request, bool isNavigation, bool isDownload, string requestInitiator, ref bool disableDefaultHandling) {
                return ResourceRequestHandler;
            }
        }
    }
}