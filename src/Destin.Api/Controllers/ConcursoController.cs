namespace Destin.Api.Controllers
{
    using ARVTech.HttpClient.Interfaces;
    using Microsoft.AspNetCore.Mvc;

    [Route("api/[controller]")]
    [ApiController]
    public class ConcursoController : ControllerBase
    {
        private readonly IHttpClientService _httpClientService;

        public ConcursoController(IHttpClientService httpClientService)
        {
            this._httpClientService = httpClientService;
        }

        [HttpGet("{numero}")]
        public async Task<IActionResult> GetConcursoAsync(int numero, CancellationToken cancellationToken) 
        { 
            var httpResponseMessage = await this._httpClientService.ExecuteAsync(HttpMethod.Get,
                $@"https://servicebus2.caixa.gov.br/portaldeloterias/api/lotofacil/{numero}",
                cancellationToken: cancellationToken);

            var httpResponseMessageContent = await httpResponseMessage.Content.ReadAsStringAsync(cancellationToken);

            return this.StatusCode((int)httpResponseMessage.StatusCode,
                httpResponseMessageContent);
        }
    }
}