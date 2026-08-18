namespace ARVTech.HttpClient
{
    using System;
    using System.Collections.Generic;
    using System.Net.Http;
    using System.Net.Http.Headers;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using ARVTech.HttpClient.Enums;
    using ARVTech.HttpClient.Interfaces;

    /// <summary>
    /// Provides a singleton HTTP client service with support for various authentication schemes.
    /// </summary>
    public class HttpClientService : IHttpClientService
    {
        private readonly HttpClient _httpClient;

        private string _mediaType = "application/json";

        private readonly string _stringEncoding = "gzip";

        private AuthenticationScheme _authenticationScheme = AuthenticationScheme.None;

        private string _nameHeaderPrefix = string.Empty;

        private string _token = string.Empty;

        private string _username = string.Empty;
        private string _password = string.Empty;

        /// <summary>
        /// Initializes a new instance of the <see cref="HttpClientService"/> class.
        /// </summary>
        /// <param name="httpClient">An HttpClient instance managed by HttpClientFactory, responsible for executing HTTP requests.</param>
        public HttpClientService(HttpClient httpClient)
        {
            this._httpClient = httpClient;
        }

        /// <summary>
        /// Configures Basic authentication using the specified username and password.
        /// </summary>
        /// <param name="username">The username to use for Basic authentication.</param>
        /// <param name="password">The password to use for Basic authentication.</param>
        public void SetBasicAuthentication(string username, string password)
        {
            this._authenticationScheme = AuthenticationScheme.Basic;

            this._username = username;
            this._password = password;
            this._nameHeaderPrefix = "Basic";

            this._token = string.Empty;
        }

        /// <summary>
        /// Configures Bearer authentication using the specified token.
        /// </summary>
        /// <param name="token">The token to use for Bearer authentication.</param>
        /// <param name="nameHeaderPrefix">The prefix for the Authorization header (default: "Bearer").</param>
        public void SetBearerAuthentication(string token, string nameHeaderPrefix = "Bearer")
        {
            this._authenticationScheme = AuthenticationScheme.Bearer;

            this._token = token;
            this._nameHeaderPrefix = nameHeaderPrefix;

            this._username = string.Empty;
            this._password = string.Empty;
        }

        /// <summary>
        /// Clears any configured authentication scheme.
        /// </summary>
        public void SetNoneAuthentication()
        {
            this._authenticationScheme = AuthenticationScheme.None;

            this._nameHeaderPrefix = string.Empty;
            this._token = string.Empty;
            this._username = string.Empty;
            this._password = string.Empty;
        }

        /// <summary>
        /// Sets the media type used in HTTP requests (default: application/json).
        /// </summary>
        /// <param name="mediaType"></param>
        public void SetMediaType(string mediaType)
        {
            this._mediaType = mediaType;
        }

        /// <summary>
        /// Sends an asynchronous HTTP request with optional content, headers, and timeout.
        /// </summary>
        /// <param name="method">The HTTP method to use (GET, POST, PUT, DELETE, etc.).</param>
        /// <param name="requestUri">The URI of the request.</param>
        /// <param name="content">The content of the request (for POST, PUT, etc.).</param>
        /// <param name="timeout">The timeout for the request in seconds.</param>
        /// <param name="customHeaders">Custom headers to include in the request.</param>
        /// <param name="cancellationToken">A cancellation token to cancel the request.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains the HTTP response message.</returns>
        public async Task<HttpResponseMessage> ExecuteAsync(
            HttpMethod method,
            string requestUri,
            string? content = null,
            double? timeout = null,
            Dictionary<string, string>? customHeaders = null,
            CancellationToken cancellationToken = default)
        {
            try
            {
                using (var httpRequestMessage = new HttpRequestMessage(
                    method,
                    requestUri))
                {
                    // Basic headers
                    httpRequestMessage.Headers.Accept.Clear();

                    httpRequestMessage.Headers.Accept.Add(
                        new MediaTypeWithQualityHeaderValue(
                            this._mediaType));

                    httpRequestMessage.Headers.AcceptEncoding.Add(
                        new StringWithQualityHeaderValue(
                            this._stringEncoding));

                    httpRequestMessage.Headers.Add(
                        "Accept",
                        "*/*");

                    // Authentication headers
                    this.AddAuthenticationHeaders(
                        httpRequestMessage);

                    // Additional headers
                    if (customHeaders != null)
                        foreach (var header in customHeaders)
                            httpRequestMessage.Headers.TryAddWithoutValidation(header.Key, header.Value);

                    // Request body
                    if (!string.IsNullOrEmpty(content))
                        httpRequestMessage.Content = new StringContent(
                            content,
                            Encoding.UTF8,
                            this._mediaType);

                    // Custom timeout
                    using (var cts = timeout.HasValue
                        ? CancellationTokenSource.CreateLinkedTokenSource(
                            cancellationToken,
                            new CancellationTokenSource(TimeSpan.FromSeconds(timeout.Value)).Token)
                        : CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
                    {
                        return await this._httpClient.SendAsync(httpRequestMessage, cts.Token);
                    }
                }
            }
            catch
            {
                throw;
            }
        }

        /// <summary>
        /// Adds authentication headers to the HTTP request based on the configured scheme.
        /// </summary>
        /// <param name="request">The HTTP request message to which authentication headers will be added.</param>
        private void AddAuthenticationHeaders(HttpRequestMessage request)
        {
            if (this._authenticationScheme == AuthenticationScheme.Basic)
            {
                var authString = $"{this._username}:{this._password}";

                var parameterBasic = Convert.ToBase64String(
                    Encoding.UTF8.GetBytes(
                        authString));

                request.Headers.Authorization = new AuthenticationHeaderValue(
                    this._nameHeaderPrefix,
                    parameterBasic);
            }
            else if (this._authenticationScheme == AuthenticationScheme.Bearer)
            {
                request.Headers.Authorization = new AuthenticationHeaderValue(
                    this._nameHeaderPrefix,
                    this._token);
            }
        }
    }
}