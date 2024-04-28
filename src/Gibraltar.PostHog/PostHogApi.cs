using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using Gibraltar.PostHog.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Gibraltar.PostHog
{
    /// <summary>
    /// The common PostHog API client.
    /// </summary>
    public class PostHogApi : IDisposable
    {
        private readonly string _apiKey;
        private readonly string _postHogCaptureUri;
        private readonly ILogger<PostHogApi> _logger;
        private readonly HttpClient _client;
        private readonly BlockingCollection<EventModel> _eventQueue;

        private readonly CancellationTokenSource? _cancellationTokenSource;

        /// <summary>
        /// The default capture URL, pointing to the US PostHog servers.
        /// </summary>
        public const string DefaultCaptureUrl = "https://app.posthog.com/capture/";

        /// <summary>
        /// Create the PostHogApi client
        /// </summary>
        /// <param name="apiKey">Your PostHog Api Key</param>
        /// <param name="client">An HttpClient to reuse for the client</param>
        /// <param name="postHogCaptureUri">Optional.  Override the PostHog capture API path.</param>
        /// <param name="logger"></param>
        /// <remarks>
        /// <para>If no API key is specified, the client will be disabled.  All calls to record data will be silently ignored.</para>
        /// <para>By default, the client will use the PostHog capture API path specified by <see cref="DefaultCaptureUrl"/>.</para></remarks>
        public PostHogApi(string apiKey, HttpClient? client = default, string? postHogCaptureUri = default, ILogger<PostHogApi>? logger = default)
        {
            _apiKey = apiKey;
            _postHogCaptureUri = postHogCaptureUri ?? DefaultCaptureUrl;
            _logger = logger ?? NullLogger<PostHogApi>.Instance;
            _client = client ?? new HttpClient();

            _eventQueue = new BlockingCollection<EventModel>();

            if (string.IsNullOrEmpty(_apiKey) == false)
            {
                Enabled = true;
                // Fire up our background task to process the queue
                _cancellationTokenSource = new CancellationTokenSource();
                Task.Run(() => ProcessQueue(_cancellationTokenSource.Token));
            }
            else
            {
                Enabled = false;
            }
        }

        /// <summary>
        /// The raw PostHog Capture call.
        /// </summary>
        /// <param name="eventName"></param>
        /// <param name="userKey"></param>
        /// <param name="properties"></param>
        /// <remarks>This is the underlying method most calls to PostHog use.</remarks>
        public void Capture(string eventName, string userKey, Dictionary<string, object>? properties = null)
        {
            if (Enabled == false)
                return;

            if (string.IsNullOrEmpty(_apiKey))
                return;

            var eventDetails = new EventModel
            {
                ApiKey = _apiKey,
                Name = eventName,
                UserKey = userKey,
                Timestamp = DateTimeOffset.UtcNow,
                Properties = properties ?? new Dictionary<string, object>()
            };

            _eventQueue.Add(eventDetails);
        }

        /// <summary>
        /// True if the client is enabled and will send data to PostHog.
        /// </summary>
        public bool Enabled { get; set; }

        /// <summary>
        /// Stop processing, waiting for pending requests to complete.
        /// </summary>
        public async Task StopProcessingAsync(CancellationToken cancellationToken = default)
        {
            // Let the queue know we aren't going to add any more so it can exit once the queue is empty.
            _eventQueue.CompleteAdding();

            while (cancellationToken.IsCancellationRequested == false && _eventQueue.Count > 0)
            {
                await Task.Delay(100, cancellationToken);
            }
        }

        /// <inheritdoc />
        public void Dispose()
        {
            _eventQueue?.CompleteAdding();
            _client?.Dispose();
            _eventQueue?.Dispose();
            _cancellationTokenSource?.Dispose();
        }

        private async Task ProcessQueue(CancellationToken cancellationToken)
        {
            var haveRecordedDisabledMessage = false;
            foreach (var eventDetails in _eventQueue.GetConsumingEnumerable(cancellationToken))
            {
                // Since the client can be enabled/disabled at runtime, we need to check each time through the loop.
                if (Enabled == false)
                {
                    if (haveRecordedDisabledMessage == false)
                    {
                        _logger?.LogWarning("PostHog API calls are disabled.  All calls to PostHog will be ignored.");
                        haveRecordedDisabledMessage = true;
                    }
                    continue;
                }

                try
                {
                    var result = await _client.PostAsJsonAsync(_postHogCaptureUri, eventDetails, cancellationToken);

                    if (result.IsSuccessStatusCode == false)
                    {
                        switch (result.StatusCode)
                        {
                            case HttpStatusCode.BadRequest:
                                _logger?.LogWarning("PostHog API call failed with status {HttpStatusCode}.  Typically the means the API key didn't map to an active project or the request payload was not formatted correctly.\r\n" +
                                                    "\r\nRaw Response:\r\n{ResponseBody}", result.StatusCode, await result.Content.ReadAsStringAsync());
                                break;
                            case HttpStatusCode.Unauthorized:
                                _logger?.LogWarning("PostHog API call failed with status {HttpStatusCode}.  Typically this means the API key was invalid.\r\n" +
                                                    "\r\nRaw Response:\r\n{ResponseBody}", result.StatusCode, await result.Content.ReadAsStringAsync());
                                break;
                            default:
                                _logger?.LogWarning("PostHog API call failed with status {HttpStatusCode}.\r\n" +
                                                    "\r\nRaw Response:\r\n{ResponseBody}", result.StatusCode, await result.Content.ReadAsStringAsync());
                                break;
                        }
                    }
                }
                catch (TaskCanceledException ex)
                {
                    _logger?.LogDebug(ex, "The request to PostHog was cancelled.");
                }
                catch (Exception ex)
                {
                    var baseException = ex.GetBaseException();
                    _logger?.LogError(ex, "Unable to send data to PostHog due to {ExceptionName}.  The request will be dropped.\r\n{ExceptionMessage}",
                        baseException.GetType().Name, baseException.Message);
                }
            }
        }
    }
}
