namespace ARVTech.HttpClient.Interfaces
{
    using System.Collections.Generic;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Interface for managing HTTP requests with support for different authentication schemes.
    /// </summary>
    public interface IHttpClientService
    {
        /// <summary>
        /// Sets Basic authentication (username and password).
        /// </summary>
        /// <param name="username">The username to use for authentication.</param>
        /// <param name="password">The password to use for authentication.</param>
        void SetBasicAuthentication(string username, string password);

        /// <summary>
        /// Sets Bearer authentication (JWT token).
        /// </summary>
        /// <param name="token">The JWT token to use for authentication.</param>
        /// <param name="headerPrefix">The prefix for the Authorization header (default: "Bearer").</param>
        void SetBearerAuthentication(string token, string headerPrefix = "Bearer");

        /// <summary>
        /// Removes any authentication scheme.
        /// </summary>
        void SetNoneAuthentication();

        /// <summary>
        /// Sets the media type (default: application/json).
        /// </summary>
        /// <param name="mediaType">The media type to set.</param>
        void SetMediaType(string mediaType);

        /// <summary>
        /// Executes an asynchronous HTTP request.
        /// </summary>
        /// <param name="method">The HTTP method to use (GET, POST, PUT, DELETE, etc.).</param>
        /// <param name="requestUri">The URI of the request.</param>
        /// <param name="content">The content of the request (for POST, PUT, etc.).</param>
        /// <param name="timeoutInSeconds">The timeout for the request in seconds.</param>
        /// <param name="customHeaders">Custom headers to include in the request.</param>
        /// <param name="cancellationToken">A cancellation token to cancel the request.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains the HTTP response message.</returns>
        Task<HttpResponseMessage> ExecuteAsync(
            HttpMethod method,
            string requestUri,
            string? content = null,
            double? timeoutInSeconds = null,
            Dictionary<string, string>? customHeaders = null,
            CancellationToken cancellationToken = default);
    }
}