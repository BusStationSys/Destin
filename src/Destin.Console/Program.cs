namespace Destin.Console
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Net.Http;
    using System.Reflection;
    using System.Threading;
    using System.Threading.Tasks;
    using ARVTech.DataAccess.Contracts.Destin.Requests;
    using ARVTech.DataAccess.Contracts.Destin.Responses;
    using ARVTech.DataAccess.Contracts.PortalLoterias.Responses;
    using ARVTech.DataAccess.DbManager;
    using ARVTech.DataAccess.DbManager.Enums;
    using ARVTech.DataAccess.Domain.Enums.Destin;
    using ARVTech.DataAccess.Service.Destin;
    using ARVTech.DataAccess.Service.Destin.Mappings;
    using ARVTech.HttpClient;
    using ARVTech.HttpClient.Interfaces;
    using ARVTech.Shared.Extensions;
    using ARVTech.Shared.Security.Implementations;
    using ARVTech.Shared.Security.Interfaces;
    using AutoMapper;
    using Destin.Console.Enums;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;
    using Newtonsoft.Json;

    public static class Program
    {
        private static IConfiguration _configuration;

        private readonly static Assembly _assembly = Assembly.GetExecutingAssembly();

        private readonly static FileVersionInfo fvi = FileVersionInfo.GetVersionInfo(_assembly.Location);

        private readonly static string _productName = fvi?.ProductName;

        private readonly static string _fileVersion = fvi.FileVersion;

        private readonly static string _arquivoLog = string.Format(
            CultureInfo.InvariantCulture,
            @"{0}\\Log{1}{2}.log",
            AppDomain.CurrentDomain.BaseDirectory,
            _assembly.GetName().Name?.Replace(
                ".Console",
                string.Empty),
            DateTime.Now.ToString("yyyyMMddHHmm"));

        private static ContextDbManager? _singletonDbManager = default;

        private static IMapper _mapper;

        private static IPasswordHasher _passwordHasher;

        private static IHttpClientService _httpClientService;

        private static string _requestUriBase = "https://servicebus2.caixa.gov.br/portaldeloterias/api/{0}";

        public static void Main(string[] args)
        {
            try
            {
                var serviceCollection = new ServiceCollection();

                // Implementação do ARGON2ID para hashing de senhas.
                serviceCollection.AddSingleton<IPepperProvider, PepperProvider>();
                serviceCollection.AddScoped<IPasswordHasher, Argon2IdPasswordHasher>();

                // Cliente HTTP para consumo de APIs externas.
                serviceCollection.AddHttpClient<IHttpClientService, HttpClientService>()
                    .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
                    {
                        AutomaticDecompression = System.Net.DecompressionMethods.GZip
                            | System.Net.DecompressionMethods.Deflate
                            | System.Net.DecompressionMethods.Brotli
                    });

                var serviceProvider = serviceCollection.BuildServiceProvider();

                _passwordHasher = serviceProvider.GetRequiredService<IPasswordHasher>();

                _httpClientService = serviceProvider.GetRequiredService<IHttpClientService>();

                var loggerFactory = LoggerFactory.Create(builder => { });

                //  Cria o mapeamento de objetos.
                //var mapperConfiguration = new MapperConfiguration(
                //    cfg =>
                //    {
                //        cfg.AddMaps(
                //            typeof(
                //                MatriculaMappingProfile).Assembly,
                //            typeof(
                //                UsuarioMappingProfile).Assembly);
                //    },
                //    loggerFactory);

                var mapperConfiguration = new MapperConfiguration(
                    cfg =>
                    {
                        cfg.AddMaps(
                            typeof(
                                ConcursoMappingProfile).Assembly);

                        cfg.AddMaps(
                            typeof(
                                ModalidadeMappingProfile).Assembly);
                    },
                    loggerFactory);

                _mapper = mapperConfiguration.CreateMapper();

                WriteConsole(
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "*** {0} [ Versão {1} ] ***",
                        _productName,
                        _fileVersion),
                    bootstrapColor: BootstrapColorEnum.Primary);

                WriteConsole("Limpando Log",
                    newLinesBefore: 2,
                    bootstrapColor: BootstrapColorEnum.Dark);

                ApagarLog();

                WriteConsole(
                    "CARREGANDO as configurações de acesso ao Destin®...",
                    newLinesBefore: 2,
                    newLinesAfter: 1,
                    bootstrapColor: BootstrapColorEnum.Dark);

                GetOrCreateConfiguration();

                _singletonDbManager = new ContextDbManager(
                    DatabaseTypeEnum.SqlServer,
                    _configuration);

                ImportarModalidades();

                ImportarConcursos();
            }
            catch (Exception ex)
            {
                WriteConsole(
                    string.Concat(
                        ex.Message,
                        " ",
                        ex.InnerException?.InnerException),
                    newLinesBefore: 1,
                    bootstrapColor: BootstrapColorEnum.Danger);
            }
            finally
            {
                WriteConsole(
                    "*** Término da execução do Destin®. ***",
                    newLinesBefore: 1,
                    bootstrapColor: BootstrapColorEnum.Dark);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <exception cref="Exception"></exception>
        private static void GetOrCreateConfiguration()
        {
            var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production"; // "Production" é o default

            // Configura o builder de configurações
            var builder = new ConfigurationBuilder()
                .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)  // arquivo padrão
                .AddJsonFile($"appsettings.{environment}.json", optional: true, reloadOnChange: true);  // arquivo específico do ambiente

            _configuration = builder.Build();

            if (_configuration is null)
                throw new Exception("[ERRO] Não foi possível carregar as configurações do Destin®.");
        }

        /// <summary>
        /// 
        /// </summary>
        private static void ApagarLog()
        {
            DateTime dataBase = DateTime.Now.AddDays(-7);

            try
            {
                string[] files = Directory.GetFiles(AppDomain.CurrentDomain.BaseDirectory, "LOG*.log");

                foreach (string file in files)
                {
                    var fs = new FileInfo(file);

                    if (fs.LastWriteTime < dataBase)
                        File.Delete(file);
                }
            }
            finally { }
        }

        /// <summary>
        /// Método que importa as Modalidades.
        /// </summary>
        private static void ImportarModalidades()
        {
            try
            {
                using (var modalidadeService = new ModalidadeService(_singletonDbManager.UnitOfWork,
                    _mapper))
                {
                    foreach (var modalidade in Enum.GetValues<Modalidade>())
                    {
                        WriteConsole($"PROCESSANDO a Modalidade {(int)modalidade} - {modalidade.GetDisplayName()}",
                            newLinesBefore: 1,
                            newLinesAfter: 1,
                            bootstrapColor: BootstrapColorEnum.Dark);

                        var modalidadeRequest = default(ModalidadeRequest);

                        var modalidadeResponse = modalidadeService.Get((int)modalidade);

                        if (modalidadeResponse is null)
                        {
                            modalidadeRequest = new ModalidadeRequest
                            {
                                Id = (int)modalidade,
                                Descricao = modalidade.GetDisplayName(),
                            };

                            modalidadeService.SaveData(
                                modalidadeRequest);

                            WriteConsole($"Modalidade {(int)modalidade} - {modalidade.GetDisplayName()} incluída com sucesso.",
                                newLinesBefore: 1,
                                newLinesAfter: 1,
                                bootstrapColor: BootstrapColorEnum.Success);
                        }
                        else
                            WriteConsole($"Modalidade {(int)modalidade} - {modalidade.GetDisplayName()} já existe.",
                                newLinesBefore: 1,
                                newLinesAfter: 1,
                                bootstrapColor: BootstrapColorEnum.Warning);
                    }
                }
            }
            catch (Exception ex)
            {
                WriteConsole(
                    string.Concat(ex.Message,
                        " ",
                        ex.InnerException?.InnerException),
                    newLinesAfter: 1,
                    newLinesBefore: 1,
                    bootstrapColor: BootstrapColorEnum.Danger);
            }
        }

        /// <summary>
        /// Método que importa os Concursos.
        /// </summary>
        private static void ImportarConcursos()
        {
            try
            {
                using (var modalidadeService = new ModalidadeService(_singletonDbManager.UnitOfWork,
                    _mapper))
                {
                    var modalidadesResponses = modalidadeService.GetAll();

                    foreach (var modalidadeResponse in modalidadesResponses)
                    {
                        if ((Modalidade)modalidadeResponse.Id != Modalidade.Lotofacil)
                            continue;

                        using (var cancellationTokenSource = new CancellationTokenSource())
                        {
                            ImportarConcursoAsync(modalidadeResponse,
                                cancellationTokenSource.Token).GetAwaiter().GetResult();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                WriteConsole(
                    string.Concat(ex.Message,
                        " ",
                        ex.InnerException?.InnerException),
                    newLinesAfter: 1,
                    newLinesBefore: 1,
                    bootstrapColor: BootstrapColorEnum.Danger);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="modalidadeResponse"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        private static async Task ImportarConcursoAsync(ModalidadeResponse modalidadeResponse, CancellationToken cancellationToken)
        {
            try
            {
                var modalidade = (Modalidade)modalidadeResponse.Id;

                int concursoInicial = modalidadeResponse.UltimoConcursoApurado.HasValue ?
                    modalidadeResponse.UltimoConcursoApurado.Value :
                    1;

                int? concursoFinal = await ObterUltimoConcursoApuradoPortalLoterias(modalidade,
                    cancellationToken);

                concursoFinal = concursoFinal.HasValue ?
                    concursoFinal.Value :
                    1;

                foreach (int concurso in Enumerable.Range(concursoInicial, concursoFinal.Value - concursoInicial + 1))
                {
                    string requestUri = string.Format(CultureInfo.InvariantCulture,
                        _requestUriBase,
                        modalidade);

                    requestUri = string.Concat(requestUri,
                        "/",
                        concurso);

                    using (var httpResponseMessage = await _httpClientService.ExecuteAsync(HttpMethod.Get,
                        requestUri,
                        cancellationToken: cancellationToken))
                    {
                        if (httpResponseMessage.IsSuccessStatusCode)
                        {
                            var httpResponseMessageContent = await httpResponseMessage.Content.ReadAsStringAsync(
                                cancellationToken);

                            var premioResponse = JsonConvert.DeserializeObject<PremioResponse>(httpResponseMessageContent);

                            if (premioResponse != null)
                            {
                                using (var concursoService = new ConcursoService(_singletonDbManager.UnitOfWork,
                                    _mapper))
                                {
                                    var concursoResponse = concursoService.GetByIdModalidadeAndNumeroAndDataApuracao(modalidadeResponse.Id,
                                        premioResponse.numero,
                                        Convert.ToDateTime(premioResponse.dataApuracao));

                                    if (concursoResponse is null)
                                    {
                                        var idConcurso = Guid.NewGuid();

                                        //  Processa as dezenas do concurso, caso existam.
                                        var dezenas = default(List<ConcursoDezenaRequest>);

                                        if (premioResponse.listaDezenas != null &&
                                            premioResponse.listaDezenas.Count > 0)
                                            dezenas = premioResponse.listaDezenas.Select(dezena => new ConcursoDezenaRequest()
                                            {
                                                Id = Guid.NewGuid(),
                                                IdConcurso = idConcurso,
                                                Dezena = short.Parse(dezena, CultureInfo.InvariantCulture),
                                            }).ToList();

                                        var concursoRequest = new ConcursoRequest()
                                        {
                                            Id = idConcurso,
                                            IdModalidade = modalidadeResponse.Id,
                                            Numero = premioResponse.numero,
                                            DataApuracao = Convert.ToDateTime(premioResponse.dataApuracao),
                                            Dezenas = dezenas,
                                        };

                                        concursoResponse = concursoService.SaveData(concursoRequest);
                                    }
                                }
                            }
                        }
                    }
                }

                //  Atualiza o último concurso apurado da Modalidade, caso haja novos concursos.
                if (concursoFinal.Value - concursoInicial > 0)
                {
                    var modalidadeRequest = new ModalidadeRequest()
                    {
                        Id = modalidadeResponse.Id,
                        Descricao = modalidadeResponse.Descricao,
                        UltimoConcursoApurado = concursoFinal,
                    };

                    await AtualizarModalidadeAsync(modalidadeRequest,
                        cancellationToken);
                }
            }
            catch (Exception ex)
            {
                WriteConsole(
                    string.Concat(ex.Message,
                        " ",
                        ex.InnerException?.InnerException),
                    newLinesAfter: 1,
                    newLinesBefore: 1,
                    bootstrapColor: BootstrapColorEnum.Danger);
            }
        }

        /// <summary>
        /// Método que atualiza a Modalidade.
        /// </summary>
        /// <param name="modalidadeResponse"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        private static async Task AtualizarModalidadeAsync(ModalidadeRequest modalidadeRequest, CancellationToken cancellationToken)
        {
            using (var modalidadeService = new ModalidadeService(_singletonDbManager.UnitOfWork,
                _mapper))
            {
                modalidadeService.SaveData(modalidadeRequest);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="modalidade"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        private static async Task<int?> ObterUltimoConcursoApuradoPortalLoterias(Modalidade modalidade, CancellationToken cancellationToken)
        {
            string requestUri = string.Format(CultureInfo.InvariantCulture,
                _requestUriBase,
                modalidade.ToString());

            using (var httpResponseMessage = await _httpClientService.ExecuteAsync(HttpMethod.Get,
                requestUri,
                cancellationToken: cancellationToken))
            {
                if (httpResponseMessage.IsSuccessStatusCode)
                {
                    var httpResponseMessageContent = await httpResponseMessage.Content.ReadAsStringAsync(
                        cancellationToken);

                    var premioResponse = JsonConvert.DeserializeObject<PremioResponse>(
                        httpResponseMessageContent);

                    if (premioResponse != null)
                        return premioResponse.numero;
                }
            }

            return null;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="texto"></param>
        /// <param name="newLinesBefore"></param>
        /// <param name="newLinesAfter"></param>
        /// <param name="bootstrapColor"></param>
        /// <param name="showDate"></param>
        private static void WriteConsole(string texto, int newLinesBefore = 0, int newLinesAfter = 0, BootstrapColorEnum bootstrapColor = BootstrapColorEnum.Secondary, bool showDate = true)
        {
            Console.ForegroundColor = GetColorFromBootstrap(bootstrapColor);

            string content = showDate ? string.Format(
                CultureInfo.InvariantCulture,
                "{0} {1}",
                DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss:ffff"),
                texto) : string.Format(
                    CultureInfo.InvariantCulture,
                    "{0}",
                    texto);

            if (newLinesBefore > 0)
            {
                for (int i = 0; i < newLinesBefore; i++)
                {
                    Console.Write(
                        System.Environment.NewLine);

                    if (!string.IsNullOrEmpty(_arquivoLog))
                        WriteFile(
                            System.Environment.NewLine);
                }
            }

            Console.Write(content);
            if (!string.IsNullOrEmpty(_arquivoLog))
                WriteFile(content);

            if (newLinesAfter > 0)
            {
                for (int i = 0; i < newLinesAfter; i++)
                {
                    Console.Write(
                        Environment.NewLine);

                    if (!string.IsNullOrEmpty(
                        _arquivoLog))
                        WriteFile(
                            Environment.NewLine);
                }
            }

            static ConsoleColor GetColorFromBootstrap(BootstrapColorEnum bootstrapColor = BootstrapColorEnum.Secondary)
            {
                Console.ResetColor();

                if (bootstrapColor != BootstrapColorEnum.Secondary)
                {
                    switch (bootstrapColor)
                    {
                        case BootstrapColorEnum.Primary:
                            Console.ForegroundColor = ConsoleColor.DarkBlue;
                            break;

                        case BootstrapColorEnum.Success:
                            Console.ForegroundColor = ConsoleColor.DarkGreen;
                            break;

                        case BootstrapColorEnum.Danger:
                            Console.ForegroundColor = ConsoleColor.DarkRed;
                            break;

                        case BootstrapColorEnum.Warning:
                            Console.ForegroundColor = ConsoleColor.DarkYellow;
                            break;

                        case BootstrapColorEnum.Info:
                            Console.ForegroundColor = ConsoleColor.Blue;
                            break;

                        case BootstrapColorEnum.Light:
                            Console.ForegroundColor = ConsoleColor.White;
                            break;

                        case BootstrapColorEnum.Dark:
                            Console.ForegroundColor = ConsoleColor.DarkGray;
                            break;
                    }
                }

                return Console.ForegroundColor;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="texto"></param>
        private static void WriteFile(string texto)
        {
            using (var streamWriter = new StreamWriter(
                _arquivoLog,
                true))
            {
                streamWriter.Write(texto);
                streamWriter.Flush();

                streamWriter.Close();
            }
        }
    }
}