namespace ARVTech.HttpClient.Enums
{ 
    /// <summary>
    /// Defines the authentication schemes supported by HttpClient.
    /// </summary>
    public enum AuthenticationScheme
    {
        /// <summary>
        /// No authentication.
        /// </summary>
        None,

        /// <summary>
        /// Basic authentication (Base64 username:password).
        /// </summary>
        Basic,

        /// <summary>
        /// Bearer authentication (JWT token).
        /// </summary>
        Bearer,
    }
}