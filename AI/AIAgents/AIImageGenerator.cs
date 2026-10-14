using Google.GenAI;
using Google.GenAI.Types;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenAI.Images;
using System.Buffers.Text;
using System.ClientModel;
using System.ClientModel.Primitives;
using System.ComponentModel;
using System.Globalization;
using System.Net.Http.Headers;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using TechnoartSDK.Extensions;
using TechnoartSDK.Models;
using TechnoartSDK.Utils;

namespace TechnoartSDK.AI.AIAgents;

/// <summary>
/// Generates images using OpenAI, Gemini or Google Imagen, with optional exact output dimensions.
/// </summary>
public class AIImageGenerator(ILogger<AIImageGenerator> logger, IOptions<AIServicesConfig> aiServicesConfig, HttpClient httpClient)
{
    #region Types

    /// <summary>
    /// Native aspect ratios supported by Imagen.
    /// </summary>
    public enum ImageAspectRatio
    {
        [Description("1:1")]
        Square,
        [Description("4:3")]
        Landscape,
        [Description("3:4")]
        Portrait,
        [Description("16:9")]
        Widescreen,
        [Description("9:16")]
        PortraitTall
    }

    /// <summary>
    /// OpenAI image generation models.
    /// </summary>
    public enum OpenAIImageModel
    {
        [Description("gpt-image-1.5")]
        GPTImage15, // OpenAI's image generation model
        [Description("gpt-image-1")]
        GPTImage1, // OpenAI's image generation model
        [Description("dall-e-2")]
        DallE2, // OpenAI's DALL-E 3 model
        [Description("dall-e-3")]
        DallE3, // OpenAI's DALL-E 3 model
        [Description(GPTImage25SunburstModel)]
        GPTImage25Sunburst,
        [Description(GPTImage25FlareModel)]
        GPTImage25Flare,
    }

    #endregion Types

    #region Methods

    private ImageClient CreateOpenAIClient(string modelName) =>
        new(modelName, new ApiKeyCredential(aiServicesConfig.Value.OpenAIApiKey),
            new OpenAI.OpenAIClientOptions
            {
                RetryPolicy = new ClientRetryPolicy(0),
                Transport = new HttpClientPipelineTransport(httpClient)
            });

    /// <summary>Validates a new paid request locally, without contacting either provider.</summary>
    public void ValidateImageRequest(string prompt, SDKEnums.ImageQualityType quality, int width, int height,
        params (string Name, ImageModel Image)[] references)
    {
        try
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(prompt);
            ArgumentNullException.ThrowIfNull(references);
            ValidateDimensions(width, height);
            var settings = aiServicesConfig.Value;
            ArgumentException.ThrowIfNullOrWhiteSpace(settings.ImageModel);
            if (!Enum.IsDefined(quality))
            {
                throw new NotSupportedException($"Unsupported image quality: {quality}.");
            }
            if (references.Length > MaximumReferenceImages)
            {
                throw new ArgumentException($"{settings.ImageService} supports at most {MaximumReferenceImages} image references. No reference was dropped.");
            }
            foreach (var reference in references)
            {
                ArgumentNullException.ThrowIfNull(reference.Image);
                if (string.IsNullOrWhiteSpace(reference.Name)
                    || reference.Image.MimeType is not ("image/png" or "image/jpeg" or "image/webp")
                    || string.IsNullOrWhiteSpace(reference.Image.Data)
                    || Convert.FromBase64String(reference.Image.Data).Length == 0)
                {
                    throw new ArgumentException("Every reference requires a name and supported image data.");
                }
            }
            switch (settings.ImageService)
            {
                case "OpenAI":
                    ArgumentException.ThrowIfNullOrWhiteSpace(settings.OpenAIApiKey);
                    if (!IsGPTImage(settings.ImageModel))
                    {
                        throw new NotSupportedException("Configured OpenAI image generation requires a gpt-image model that supports reference edits.");
                    }
                    GetOpenAIQuality(settings.ImageModel, quality);
                    var nativeSize = GetOpenAIImageSize(settings.ImageModel, width, height);
                    var composedPrompt = AddCompositionPrompt(prompt, width, height, nativeSize);
                    ValidateOpenAIPrompt(composedPrompt, settings.ImageModel);
                    switch (settings.OpenAIImageTransport)
                    {
                        case "Images":
                            break;
                        case "Responses":
                            CreateOpenAIResponsesRequest(composedPrompt, settings.ImageModel,
                                settings.OpenAIImageResponsesModel, quality, nativeSize, references);
                            break;
                        default:
                            throw new NotSupportedException($"Unsupported OpenAI image transport: {settings.OpenAIImageTransport}.");
                    }
                    break;
                case "Gemini":
                    ArgumentException.ThrowIfNullOrWhiteSpace(settings.GoogleApiKey);
                    if (!settings.ImageModel.StartsWith("gemini-", StringComparison.Ordinal))
                    {
                        throw new NotSupportedException("Configured Gemini image generation requires a Gemini image model ID.");
                    }
                    CreateGeminiRequest(AddCompositionPrompt(prompt, width, height), GetGeminiAspectRatio(width, height),
                        quality, references);
                    break;
                default:
                    throw new NotSupportedException($"Unsupported image service: {settings.ImageService}.");
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Invalid image-generation request; no provider was contacted");
            throw;
        }
    }

    /// <summary>Submits once to the configured image service and model, retaining every named reference.</summary>
    public Task<ImageModel?> CreateImageAsync(string name, string prompt, SDKEnums.ImageQualityType quality,
        int width, int height, CancellationToken ct = default, params (string Name, ImageModel Image)[] references)
    {
        ct.ThrowIfCancellationRequested();
        ValidateImageRequest(prompt, quality, width, height, references);
        var settings = aiServicesConfig.Value;
        return CreateExactImageAsync(name, prompt, width, height, async (composed, nativeSize, token) =>
            settings.ImageService switch
            {
                "Gemini" => await CreateGeminiImageCore(name, composed, GetGeminiAspectRatio(width, height), quality, references, token),
                "OpenAI" when settings.OpenAIImageTransport == "Responses" => await CreateOpenAIResponsesImageCore(
                    name, composed, settings.ImageModel,
                    settings.OpenAIImageResponsesModel ?? throw new InvalidOperationException("An explicit Responses dispatcher is required."),
                    quality, nativeSize ?? throw new InvalidOperationException("An OpenAI native image size is required."),
                    references, token),
                "OpenAI" when settings.OpenAIImageTransport == "Images" && references.Length == 0 =>
                    await CreateOpenAIImageCore(name, composed, settings.ImageModel,
                    quality, nativeSize, token),
                "OpenAI" when settings.OpenAIImageTransport == "Images" =>
                    await CreateOpenAIImageByRefrenceCore(name, composed, settings.ImageModel,
                    quality, nativeSize, token, references),
                _ => throw new NotSupportedException($"Unsupported image service or transport: {settings.ImageService}, {settings.OpenAIImageTransport}.")
            }, ct, settings.ImageService == "OpenAI" ? settings.ImageModel : null);
    }

    private async Task<ImageModel> CreateOpenAIResponsesImageCore(string name, string prompt, string model,
        string dispatcherModel, SDKEnums.ImageQualityType quality, (int Width, int Height) nativeSize,
        (string Name, ImageModel Image)[] references, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var requestBody = CreateOpenAIResponsesRequest(prompt, model, dispatcherModel, quality, nativeSize, references);
        var metadata = new ImageGenerationMetadata
        {
            Provider = "OpenAI",
            Transport = "Responses",
            RequestedDispatcherModel = dispatcherModel,
            RequestedImageModel = model
        };
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.openai.com/v1/responses");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", aiServicesConfig.Value.OpenAIApiKey);
            request.Content = new StringContent(requestBody, Encoding.UTF8, "application/json");
            logger.LogInformation("Starting OpenAI Responses image generation for {Name}: {DispatcherModel}, {ImageModel}, {Quality}, {ReferenceCount} references",
                name, dispatcherModel, model, quality, references.Length);
            // The caller's typed client must also disable retries and request/response body logging.
            using var response = await httpClient.SendAsync(request, ct);
            metadata = metadata with
            {
                RequestId = response.Headers.TryGetValues("x-request-id", out var requestIds) ? requestIds.FirstOrDefault() : null
            };
            response.EnsureSuccessStatusCode();
            var body = JObject.Parse(await response.Content.ReadAsStringAsync(ct));
            metadata = metadata with
            {
                ResponseId = ReadResponseString(body["id"], "response.id"),
                ActualDispatcherModel = ReadResponseString(body["model"], "response.model")
            };
            metadata = metadata with { DispatcherUsage = ReadImageGenerationUsage(body["usage"], includeCacheUsage: true) };
            if (body["output"] is not JArray output || output.Any(item => item is not JObject))
            {
                throw new InvalidOperationException("OpenAI Responses returned an invalid output collection.");
            }
            var items = output.OfType<JObject>().ToArray();
            var imageCalls = items.Where(item => ReadResponseString(item["type"], "output.type") == "image_generation_call").ToArray();
            if (imageCalls.Length == 1)
            {
                metadata = metadata with
                {
                    ImageCallId = ReadResponseString(imageCalls[0]["id"], "image_generation_call.id"),
                    ActualImageModel = ReadResponseString(imageCalls[0]["model"], "image_generation_call.model")
                };
                // Tool usage/model are not guaranteed by the public schema; never substitute the outer usage or tool definition.
                metadata = metadata with { ImageUsage = ReadImageGenerationUsage(imageCalls[0]["usage"], includeCacheUsage: false) };
            }
            if (ReadResponseString(body["status"], "response.status") != "completed"
                || body["error"] is { Type: not JTokenType.Null }
                || body["incomplete_details"] is { Type: not JTokenType.Null })
            {
                throw new InvalidOperationException("OpenAI Responses did not complete the image request successfully.");
            }
            if (!MatchesRequestedOpenAIModel(dispatcherModel, metadata.ActualDispatcherModel)
                || metadata.ActualImageModel is { } actualImageModel && !MatchesRequestedOpenAIModel(model, actualImageModel))
            {
                throw new InvalidOperationException("OpenAI Responses returned a different or missing requested model.");
            }
            foreach (var item in items)
            {
                var type = ReadResponseString(item["type"], "output.type");
                if (type is not ("image_generation_call" or "message" or "reasoning")
                    || ReadResponseString(item["status"], "output.status") is { } status && status != "completed")
                {
                    throw new InvalidOperationException("OpenAI Responses returned an unexpected tool or incomplete output.");
                }
                if (type == "message")
                {
                    if (item["content"] is not JArray messageContent || messageContent.Any(part => part is not JObject))
                    {
                        throw new InvalidOperationException("OpenAI Responses returned invalid message content.");
                    }
                    if (messageContent.OfType<JObject>().Any(part => ReadResponseString(part["type"], "content.type") == "refusal"))
                    {
                        throw new InvalidOperationException("OpenAI Responses refused the image request.");
                    }
                }
            }
            if (imageCalls.Length != 1 || ReadResponseString(imageCalls[0]["status"], "image_generation_call.status") != "completed")
            {
                throw new InvalidOperationException("OpenAI Responses must return exactly one completed image-generation call.");
            }
            var imageCall = imageCalls[0];
            ValidateResponseSetting(imageCall, "action", references.Length == 0 ? "generate" : "edit");
            ValidateResponseSetting(imageCall, "size", $"{nativeSize.Width}x{nativeSize.Height}");
            ValidateResponseSetting(imageCall, "quality", GetOpenAIQuality(model, quality));
            ValidateResponseSetting(imageCall, "output_format", "png");
            var imageData = ReadResponseString(imageCall["result"], "image_generation_call.result");
            if (string.IsNullOrWhiteSpace(imageData))
            {
                throw new InvalidOperationException("OpenAI Responses returned no image data.");
            }
            ct.ThrowIfCancellationRequested();
            ValidateResponsesImageData(Convert.FromBase64String(imageData), nativeSize);
            ct.ThrowIfCancellationRequested();
            return new ImageModel { Data = imageData, MimeType = "image/png", GenerationMetadata = metadata };
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex) when (ex is HttpRequestException or IOException or JsonException or InvalidOperationException
            or ArgumentException or FormatException or OverflowException or System.Runtime.InteropServices.ExternalException)
        {
            var failure = new ImageGenerationException("OpenAI Responses image generation failed; no retry was made.", metadata, ex);
            logger.LogError(failure, "OpenAI Responses image generation failed for {Name}, request {RequestId}; it will not be replayed",
                name, metadata.RequestId);
            throw failure;
        }
    }

    /// <summary>
    /// Generates an Imagen image at the requested native aspect ratio.
    /// </summary>
    public async Task<ImageModel?> CreateImagen(string name, string text2ImagePrompt, ImageAspectRatio aspectRatio, CancellationToken ct = default)
    {
        logger.LogInformation("Starting image generation for character: {Name}", name);

        var googleAI = new Client(apiKey: aiServicesConfig.Value.GoogleApiKey);
        var generateImagesConfig = new GenerateImagesConfig
        {
            NumberOfImages = 1,
            AspectRatio = aspectRatio.ToDescriptionString(),
            SafetyFilterLevel = SafetyFilterLevel.BlockLowAndAbove,
            PersonGeneration = PersonGeneration.AllowAll,
            IncludeSafetyAttributes = false,
            IncludeRaiReason = false,
            OutputMimeType = "image/jpeg",
        };
        //var vertextAI = new VertexAI(apiKey: _APIKEY);

        //var vModel = googleAI.GenerativeModel(model: Model.Imagen4);
        ct.ThrowIfCancellationRequested();
        try
        {
            var response = await googleAI.Models.GenerateImagesAsync(
              model: aiServicesConfig.Value.ImagenModel,
              prompt: text2ImagePrompt,
              config: generateImagesConfig,
              cancellationToken: ct
            );
            ct.ThrowIfCancellationRequested();
            // Do something with the generated image
            var image = response.GeneratedImages?.FirstOrDefault()?.Image;
            //var res = await vModel.GenerateImages(text2ImagePrompt, aspectRatio: aspectRatio, personGeneration: PersonGeneration.AllowAll);
            //var img = res.Images.FirstOrDefault();
            return image != null ? new ImageModel { Data = Convert.ToBase64String(image.ImageBytes!), MimeType = image.MimeType! } : null;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, $"Error in creating character \"{name}\": {ex.Message}");
        }
        return null;

    }

    /// <summary>
    /// Generates an Imagen image and center-crops it to exact PNG dimensions, each from 64 through 4096.
    /// </summary>
    public Task<ImageModel?> CreateImagen(string name, string text2ImagePrompt, int width, int height, CancellationToken ct = default)
    {
        return CreateExactImageAsync(name, text2ImagePrompt, width, height,
            (prompt, _, token) => CreateImagen(name, prompt, GetImagenAspectRatio(width, height), token), ct);
    }

    /// <summary>
    /// Generates one Gemini image using the configured model and the supplied PNG references.
    /// </summary>
    public async Task<string?> CreateGeminiImage(string name, string text2ImagePrompt, string aspectRatio, CancellationToken ct = default, params string[] imagesData)
    {
        return (await CreateGeminiImageCore(name, text2ImagePrompt, aspectRatio, SDKEnums.ImageQualityType.High,
            imagesData.Select((data, index) => ($"Reference {index + 1}", new ImageModel { Data = data, MimeType = "image/png" })).ToArray(), ct)).Data;
    }

    private string CreateGeminiRequest(string prompt, string aspectRatio, SDKEnums.ImageQualityType quality,
        (string Name, ImageModel Image)[] references)
    {
        if (references.Length > MaximumGeminiReferenceImages)
        {
            throw new ArgumentException($"Gemini supports at most {MaximumGeminiReferenceImages} reference images.");
        }
        var input = new JArray(new JObject { ["type"] = "text", ["text"] = prompt });
        foreach (var (name, image) in references)
        {
            input.Add(new JObject { ["type"] = "text", ["text"] = name });
            input.Add(new JObject { ["type"] = "image", ["mime_type"] = image.MimeType, ["data"] = image.Data });
        }
        var body = JsonConvert.SerializeObject(new
        {
            model = aiServicesConfig.Value.ImageModel,
            input,
            response_format = new { type = "image", aspect_ratio = aspectRatio, image_size = "2K" },
            generation_config = new
            {
                thinking_level = quality switch
                {
                    SDKEnums.ImageQualityType.Low => "minimal",
                    SDKEnums.ImageQualityType.Medium => "medium",
                    _ => "high"
                },
                thinking_summaries = "none"
            },
            store = false
        });
        if (Encoding.UTF8.GetByteCount(body) > 20_000_000)
        {
            throw new ArgumentException("Gemini's 20 MB inline request limit would be exceeded. No references were dropped or resized.");
        }
        return body;
    }

    private async Task<ImageModel> CreateGeminiImageCore(string name, string prompt, string aspectRatio,
        SDKEnums.ImageQualityType quality, (string Name, ImageModel Image)[] references, CancellationToken ct)
    {
        try
        {
            ct.ThrowIfCancellationRequested();
            using var request = new HttpRequestMessage(HttpMethod.Post, "https://generativelanguage.googleapis.com/v1beta/interactions");
            request.Headers.Add("x-goog-api-key", aiServicesConfig.Value.GoogleApiKey);
            request.Content = new StringContent(CreateGeminiRequest(prompt, aspectRatio, quality, references),
                Encoding.UTF8, "application/json");
            logger.LogInformation("Starting Gemini image generation for {Name}: {Model}, {Quality}, {ReferenceCount} references",
                name, aiServicesConfig.Value.ImageModel, quality, references.Length);
            // The consuming application's typed client must also disable transport-level retries.
            using var response = await httpClient.SendAsync(request, ct);
            response.EnsureSuccessStatusCode();
            var body = JObject.Parse(await response.Content.ReadAsStringAsync(ct));
            if ((string?)body["status"] != "completed" || (string?)body["model"] != aiServicesConfig.Value.ImageModel)
            {
                throw new InvalidOperationException("Gemini did not complete with the requested image model. No retry was made.");
            }
            var outputs = body["steps"]?.OfType<JObject>().Where(step => (string?)step["type"] == "model_output")
                .SelectMany(step => step["content"]?.OfType<JObject>() ?? [])
                .Where(part => (string?)part["type"] == "image").ToArray() ?? [];
            if (outputs.Length != 1 || (string?)outputs[0]["mime_type"] is not ("image/png" or "image/jpeg" or "image/webp")
                || string.IsNullOrWhiteSpace((string?)outputs[0]["data"]))
            {
                throw new InvalidOperationException("Gemini must return exactly one supported output image. No retry was made.");
            }
            ct.ThrowIfCancellationRequested();
            return new ImageModel { Data = (string)outputs[0]["data"]!, MimeType = (string)outputs[0]["mime_type"]! };
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Gemini image generation failed for {Name}; it will not be replayed", name);
            throw;
        }
    }

    /// <summary>
    /// Generates an OpenAI image using the existing landscape request defaults.
    /// </summary>
    public Task<ImageModel?> CreateOpenAIImage(string name, string text2ImagePrompt, OpenAIImageModel model, SDKEnums.ImageQualityType quality, CancellationToken ct = default)
    {
        return CreateOpenAIImageCore(name, text2ImagePrompt, model.ToDescriptionString(), quality, null, ct);
    }

    /// <summary>
    /// Generates an OpenAI image and resizes it to exact PNG dimensions, each from 64 through 4096,
    /// center-cropping only when the selected native aspect ratio differs.
    /// </summary>
    public Task<ImageModel?> CreateOpenAIImage(string name, string text2ImagePrompt, OpenAIImageModel model,
        SDKEnums.ImageQualityType quality, int width, int height, CancellationToken ct = default)
    {
        return CreateExactImageAsync(name, text2ImagePrompt, width, height,
            (prompt, nativeSize, token) => CreateOpenAIImageCore(name, prompt, model.ToDescriptionString(), quality, nativeSize, token),
            ct, model.ToDescriptionString());
    }

    private async Task<ImageModel?> CreateOpenAIImageCore(string name, string text2ImagePrompt,
        string model, SDKEnums.ImageQualityType quality, (int Width, int Height)? nativeSize, CancellationToken ct)
    {
        try
        {
            ValidateOpenAIPrompt(text2ImagePrompt, model);
            var modelName = model;
            var qualityName = GetOpenAIQuality(model, quality);
            ImageClient client = CreateOpenAIClient(modelName);

            logger.LogInformation("Starting OpenAI image generation for {Name}: {Model}, {Quality}", name, modelName, qualityName);
            ImageGenerationOptions op = new()
            {

                Quality = IsGPTImage(model)
                    ? new GeneratedImageQuality(qualityName)
                    : new GeneratedImageQuality(model == "dall-e-2" && nativeSize.HasValue ? "standard" : "hd"),

                Size = nativeSize is { } size
                    ? new GeneratedImageSize(size.Width, size.Height)
                    : IsGPTImage(model)
                        ? new GeneratedImageSize(1536, 1024)
                        : new GeneratedImageSize(1792, 1024)
            };

            ct.ThrowIfCancellationRequested();
            var res = await client.GenerateImageAsync(text2ImagePrompt, op, ct);
            ct.ThrowIfCancellationRequested();

            ImageModel imgModel = null!;
            if (res.Value.ImageBytes is not null)
            {
                imgModel = new ImageModel
                {
                    Data = Convert.ToBase64String(res.Value.ImageBytes),
                    MimeType = "image/png"
                };
            }
            else if (res.Value.ImageUri is not null)
            {
                async Task<byte[]> DownloadAsync(Uri url)
                {
                    using HttpClient client = new HttpClient();
                    return await client.GetByteArrayAsync(url, ct);
                }
                imgModel = new ImageModel
                {
                    Data = Convert.ToBase64String(await DownloadAsync(res.Value.ImageUri)),
                    MimeType = "image/png"
                };
            }

            //File.WriteAllBytes("result.png", img.ImageBytes);
            return imgModel ?? throw new InvalidOperationException("OpenAI image generation returned no image data.");
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, ex.Message);
            throw;
        }
    }

    /// <summary>
    /// Generates an OpenAI image from references using the existing landscape request defaults.
    /// </summary>
    public Task<ImageModel?> CreateOpenAIImageByRefrence(string name, string text2ImagePrompt,
        OpenAIImageModel model, SDKEnums.ImageQualityType quality,
        CancellationToken ct = default,
        params (string Name, ImageModel Image)[] imagesData)
    {
        return CreateOpenAIImageByRefrenceCore(name, text2ImagePrompt, model.ToDescriptionString(), quality, null, ct, imagesData);
    }

    /// <summary>
    /// Generates an OpenAI image from references and resizes it to exact PNG dimensions, each from 64 through 4096,
    /// center-cropping only when the selected native aspect ratio differs.
    /// </summary>
    public Task<ImageModel?> CreateOpenAIImageByRefrence(string name, string text2ImagePrompt,
        OpenAIImageModel model, SDKEnums.ImageQualityType quality, int width, int height,
        CancellationToken ct = default, params (string Name, ImageModel Image)[] imagesData)
    {
        return CreateExactImageAsync(name, text2ImagePrompt, width, height,
            (prompt, nativeSize, token) => CreateOpenAIImageByRefrenceCore(name, prompt, model.ToDescriptionString(), quality,
                nativeSize, token, imagesData), ct, model.ToDescriptionString());
    }

    private async Task<ImageModel?> CreateOpenAIImageByRefrenceCore(string name, string text2ImagePrompt,
        string model, SDKEnums.ImageQualityType quality, (int Width, int Height)? nativeSize,
        CancellationToken ct, params (string Name, ImageModel Image)[] imagesData)
    {
        try
        {
            var qualityName = GetOpenAIQuality(model, quality);
            if (IsGPTImage(model) && imagesData.Length is < 1 or > MaximumOpenAIReferenceImages)
            {
                throw new ArgumentException($"OpenAI image editing requires between 1 and {MaximumOpenAIReferenceImages} reference images.", nameof(imagesData));
            }
            ValidateOpenAIPrompt(text2ImagePrompt, model);
            using MultipartFormDataContent content = new(CreateBoundary());
            var modelName = model;
            var client = CreateOpenAIClient(modelName);
            content.Add(new StringContent(modelName), "model");

            content.Add(nativeSize is { } size
                ? new StringContent($"{size.Width}x{size.Height}")
                : IsGPTImage(model)
                    ? new StringContent("1536x1024")
                    : new StringContent("1792x1024")
                , "size");
            if (IsGPTImage(model))
            {
                content.Add(new StringContent(qualityName), "quality");
            }
            else if (nativeSize.HasValue && model == "dall-e-2")
            {
                content.Add(new StringContent("b64_json"), "response_format");
            }

            content.Add(new StringContent(text2ImagePrompt), "prompt");

            foreach (var imageInfo in imagesData)
            {
                if (string.IsNullOrEmpty(imageInfo.Image.Data) || string.IsNullOrEmpty(imageInfo.Image.MimeType))
                {
                    throw new ArgumentException($"Image data or MIME type is empty for image {imageInfo.Name}.");
                }
                var imgStream = new MemoryStream(Convert.FromBase64String(imageInfo.Image.Data));

                content.Add(new StreamContent(imgStream) { Headers = { ContentType = new MediaTypeHeaderValue(imageInfo.Image.MimeType) } }, "image[]", imageInfo.Name);
            }



            BC bc = new(content);
            logger.LogInformation("Starting OpenAI image editing for {Name}: {Model}, {Quality}, {ReferenceCount} references",
                name, modelName, qualityName, imagesData.Length);
            ct.ThrowIfCancellationRequested();
            //var res = c.GenerateImageEdit(name, text2ImagePrompt);
            var result = await client.GenerateImageEditsAsync(bc, content.Headers.ContentType.ToString(),
                new RequestOptions { CancellationToken = ct });
            ct.ThrowIfCancellationRequested();
#pragma warning disable OPENAI001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
            var img = ((GeneratedImageCollection)result).FirstOrDefault()
                ?? throw new InvalidOperationException("OpenAI image editing returned no image.");
#pragma warning restore OPENAI001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
            if (img.ImageBytes is null)
            {
                throw new InvalidOperationException("OpenAI image editing returned no image data.");
            }
            var imgModel = new ImageModel
            {
                Data = Convert.ToBase64String(img.ImageBytes),
                MimeType = "image/png"
            };
            //File.WriteAllBytes("result.png", img.ImageBytes);
            return imgModel;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, ex.Message);
            throw;
        }
    }

    private async Task<ImageModel?> CreateExactImageAsync(string name, string text2ImagePrompt, int width, int height,
        Func<string, (int Width, int Height)?, CancellationToken, Task<ImageModel?>> generateImage, CancellationToken ct,
        string? openAIModel = null)
    {
        ct.ThrowIfCancellationRequested();
        ImageGenerationMetadata? generationMetadata = null;
        try
        {
            ValidateDimensions(width, height);
            var nativeSize = openAIModel is null ? ((int Width, int Height)?)null : GetOpenAIImageSize(openAIModel, width, height);
            var prompt = AddCompositionPrompt(text2ImagePrompt, width, height, nativeSize);
            var image = await generateImage(prompt, nativeSize, ct);
            generationMetadata = image?.GenerationMetadata;
            ct.ThrowIfCancellationRequested();
            if (image is null)
            {
                return null;
            }

            // Normalize outside the provider's nullable failure path; corrupt output must not appear successful.
            byte[] imageBytes = Convert.FromBase64String(image.Data);
            ct.ThrowIfCancellationRequested();
            byte[] normalizedBytes = ImageUtils.ResizeExact(imageBytes, width, height);
            ct.ThrowIfCancellationRequested();
            var result = image with { Data = Convert.ToBase64String(normalizedBytes), MimeType = "image/png" };
            ct.ThrowIfCancellationRequested();
            return result;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error generating exact-size image for {Name} at {Width}x{Height}", name, width, height);
            if (generationMetadata is not null && ex is not ImageGenerationException)
            {
                throw new ImageGenerationException("Image generation completed, but output normalization failed; no retry was made.",
                    generationMetadata, ex);
            }
            throw;
        }
    }

    #endregion Methods

    #region Fields

    private int MaximumReferenceImages => GetMaximumReferenceImages(aiServicesConfig.Value.ImageService);

    #endregion Fields

    #region Static Fields

    /// <summary>Maximum image inputs per GPT Image edit, including character, location and composition references.</summary>
    public const int MaximumOpenAIReferenceImages = 16;

    /// <summary>Maximum Unicode characters in the final GPT Image prompt, including composition guidance.</summary>
    public const int MaximumOpenAIPromptCharacters = 32_000;

    /// <summary>Maximum image inputs to Gemini image generation or editing.</summary>
    public const int MaximumGeminiReferenceImages = 14;

    /// <summary>GPT Image 2.5 Sunburst model ID.</summary>
    public const string GPTImage25SunburstModel = "gpt-image-2.5-sunburst";

    /// <summary>GPT Image 2.5 Flare model ID.</summary>
    public const string GPTImage25FlareModel = "gpt-image-2.5-flare";

    private const string GPTImage25SnapshotSuffix = "-2026-09-08";
    private static readonly string[] GPTImage25Models = [GPTImage25SunburstModel, GPTImage25FlareModel];

    private const int CustomSizeStep = 16;
    private const int MaximumCustomSizeEdge = 3840;
    private const int MinimumCustomSizePixels = 655_360;
    private const int MaximumCustomSizePixels = 8_294_400;
    private const int MaximumAutomaticCustomSizePixels = 3_686_400;
    private const int MaximumOpenAIResponsesRequestBytes = 512 * 1024 * 1024;
    private static readonly byte[] PngSignature = [137, 80, 78, 71, 13, 10, 26, 10];

    #endregion Static Fields

    #region Static Methods

    private static string CreateOpenAIResponsesRequest(string prompt, string model, string? dispatcherModel,
        SDKEnums.ImageQualityType quality, (int Width, int Height) nativeSize, (string Name, ImageModel Image)[] references)
    {
        // The existing exact-image normalizer uses System.Drawing; reject unsupported hosts before paid work.
        if (!OperatingSystem.IsWindowsVersionAtLeast(6, 1))
        {
            throw new PlatformNotSupportedException("Exact Responses image decoding and normalization require Windows.");
        }
        ArgumentException.ThrowIfNullOrWhiteSpace(dispatcherModel);
        if (dispatcherModel is not ("gpt-5.4-mini" or "gpt-5.4-mini-2026-03-17"))
        {
            throw new NotSupportedException("Responses image generation requires the explicitly supported gpt-5.4-mini dispatcher or its 2026-03-17 snapshot.");
        }
        if (!IsSunburst(model))
        {
            throw new NotSupportedException("The opt-in Responses transport currently supports only the verified Sunburst model and its 2026-09-08 snapshot.");
        }
        ValidateOpenAIPrompt(prompt, model);
        var content = new JArray();
        // Stable references precede changing prompt text, but their original order and names always take priority over caching.
        foreach (var (name, image) in references)
        {
            content.Add(new JObject { ["type"] = "input_text", ["text"] = name });
            content.Add(new JObject { ["type"] = "input_image", ["image_url"] = $"data:{image.MimeType};base64,{image.Data}", ["detail"] = "auto" });
        }
        content.Add(new JObject { ["type"] = "input_text", ["text"] = prompt });
        // Application-wide JSON defaults must not omit false control flags or add type metadata to the wire protocol.
        var body = JObject.FromObject(new
        {
            model = dispatcherModel,
            instructions = "Use the image generation tool exactly once, following the supplied prompt and composition requirements. "
                + "Preserve every reference image, its name, its attachment order, and the numbered roles in the prompt. "
                + "Do not omit or reorder references. Return only the generated image.",
            input = new[] { new { role = "user", content } },
            tools = new[]
            {
                new
                {
                    type = "image_generation",
                    model,
                    action = references.Length == 0 ? "generate" : "edit",
                    size = $"{nativeSize.Width}x{nativeSize.Height}",
                    quality = GetOpenAIQuality(model, quality),
                    output_format = "png",
                    partial_images = 0
                }
            },
            tool_choice = new { type = "image_generation" },
            max_tool_calls = 1,
            parallel_tool_calls = false,
            reasoning = new { effort = "none" },
            truncation = "disabled",
            store = false,
            stream = false
        }, JsonSerializer.Create()).ToString(Formatting.None);
        if (Encoding.UTF8.GetByteCount(body) > MaximumOpenAIResponsesRequestBytes)
        {
            throw new ArgumentException("The Responses 512 MB request limit would be exceeded. No references were dropped or resized.");
        }
        return body;
    }

    private static bool MatchesRequestedOpenAIModel(string requestedModel, string? actualModel)
    {
        static string Snapshot(string model) => model switch
        {
            "gpt-5.4-mini" => "gpt-5.4-mini-2026-03-17",
            _ when GPTImage25Models.Contains(model) => model + GPTImage25SnapshotSuffix,
            _ => model
        };
        return actualModel is not null && Snapshot(requestedModel) == Snapshot(actualModel);
    }

    private static string? ReadResponseString(JToken? value, string field)
    {
        if (value is null || value.Type == JTokenType.Null)
        {
            return null;
        }
        if (value.Type != JTokenType.String)
        {
            throw new InvalidOperationException($"OpenAI Responses field {field} must be a string.");
        }
        return value.Value<string>();
    }

    private static void ValidateResponseSetting(JObject imageCall, string setting, string expected)
    {
        if (ReadResponseString(imageCall[setting], $"image_generation_call.{setting}") is { } actual && actual != expected)
        {
            throw new InvalidOperationException($"OpenAI Responses returned an unexpected image {setting}.");
        }
    }

    private static ImageGenerationTokenUsage? ReadImageGenerationUsage(JToken? value, bool includeCacheUsage)
    {
        var usage = ReadUsageObject(value);
        if (usage is null)
        {
            return null;
        }
        var inputDetails = ReadUsageObject(usage["input_tokens_details"]);
        var outputDetails = ReadUsageObject(usage["output_tokens_details"]);
        return new ImageGenerationTokenUsage
        {
            InputTokens = ReadTokenCount(usage["input_tokens"]),
            CachedInputTokens = includeCacheUsage ? ReadTokenCount(inputDetails?["cached_tokens"]) : null,
            CacheWriteInputTokens = includeCacheUsage ? ReadTokenCount(inputDetails?["cache_write_tokens"]) : null,
            TextInputTokens = ReadTokenCount(inputDetails?["text_tokens"]),
            ImageInputTokens = ReadTokenCount(inputDetails?["image_tokens"]),
            OutputTokens = ReadTokenCount(usage["output_tokens"]),
            TextOutputTokens = ReadTokenCount(outputDetails?["text_tokens"]),
            ImageOutputTokens = ReadTokenCount(outputDetails?["image_tokens"]),
            ReasoningOutputTokens = ReadTokenCount(outputDetails?["reasoning_tokens"]),
            TotalTokens = ReadTokenCount(usage["total_tokens"])
        };
    }

    private static JObject? ReadUsageObject(JToken? value)
    {
        if (value is null || value.Type == JTokenType.Null)
        {
            return null;
        }
        return value as JObject ?? throw new InvalidOperationException("OpenAI Responses returned malformed token usage.");
    }

    private static long? ReadTokenCount(JToken? value)
    {
        if (value is null || value.Type == JTokenType.Null)
        {
            return null;
        }
        if (value.Type != JTokenType.Integer
            || !long.TryParse(value.ToString(Formatting.None), NumberStyles.Integer, CultureInfo.InvariantCulture, out long count))
        {
            throw new InvalidOperationException("OpenAI Responses token counts must be 64-bit integers.");
        }
        if (count < 0)
        {
            throw new InvalidOperationException("OpenAI Responses token counts cannot be negative.");
        }
        return count;
    }

    private static void ValidateResponsesImageData(byte[] data, (int Width, int Height) nativeSize)
    {
        if (!OperatingSystem.IsWindowsVersionAtLeast(6, 1))
        {
            throw new PlatformNotSupportedException("Exact Responses image decoding and normalization require Windows.");
        }
        if (!data.AsSpan().StartsWith(PngSignature))
        {
            throw new InvalidOperationException("OpenAI Responses did not return PNG image data.");
        }
        using var stream = new MemoryStream(data, writable: false);
        using var image = System.Drawing.Image.FromStream(stream, useEmbeddedColorManagement: false, validateImageData: true);
        if (image.Width != nativeSize.Width || image.Height != nativeSize.Height)
        {
            throw new InvalidOperationException("OpenAI Responses did not return the requested native image dimensions.");
        }
    }

    /// <summary>Returns the provider's reference capacity without resolving or validating credentials.</summary>
    public static int GetMaximumReferenceImages(string service) => service switch
    {
        "OpenAI" => MaximumOpenAIReferenceImages,
        "Gemini" => MaximumGeminiReferenceImages,
        _ => throw new NotSupportedException($"Unsupported image service: {service}.")
    };

    /// <summary>
    /// Selects the supported OpenAI size that preserves the most image area after center cropping.
    /// Requested dimensions must each be from 64 through 4096.
    /// GPT Image 2.5 preserves valid native requests; replacements use at most 3,686,400 pixels,
    /// preferring a full-frame resize and avoiding upscaling when that ratio permits it.
    /// Unsupported targets may still require a final upscale or centered crop.
    /// </summary>
    public static (int Width, int Height) GetOpenAIImageSize(OpenAIImageModel model, int width, int height)
    {
        if (!Enum.IsDefined(model))
        {
            throw new NotSupportedException($"Unsupported OpenAI image model: {model}.");
        }
        return GetOpenAIImageSize(model.ToDescriptionString(), width, height);
    }

    private static (int Width, int Height) GetOpenAIImageSize(string model, int width, int height)
    {
        ValidateDimensions(width, height);
        if (IsGPTImage25(model))
        {
            return GetCustomImageSize(width, height);
        }
        (int Width, int Height)[] sizes = model switch
        {
            _ when IsGPTImage(model) =>
                [(1024, 1024), (1536, 1024), (1024, 1536)],
            "dall-e-3" => [(1024, 1024), (1792, 1024), (1024, 1792)],
            "dall-e-2" => [(1024, 1024)],
            _ => throw new NotSupportedException($"Unsupported OpenAI image model: {model}.")
        };
        return GetClosestSize(width, height, sizes);
    }

    private static (int Width, int Height) GetCustomImageSize(int width, int height)
    {
        if (IsValidCustomSize(width, height, MaximumCustomSizePixels))
        {
            return (width, height);
        }

        var bestSize = (Width: 1024, Height: 1024);
        var bestScore = (Crop: double.PositiveInfinity, Upscale: true, ScaleDistance: double.PositiveInfinity);
        double targetRatio = (double)width / height;
        // Search the bounded native grid without allocating images or opting into experimental output.
        for (int nativeWidth = CustomSizeStep; nativeWidth <= MaximumCustomSizeEdge; nativeWidth += CustomSizeStep)
        {
            for (int nativeHeight = CustomSizeStep; nativeHeight <= MaximumCustomSizeEdge; nativeHeight += CustomSizeStep)
            {
                if (!IsValidCustomSize(nativeWidth, nativeHeight, MaximumAutomaticCustomSizePixels))
                {
                    continue;
                }
                double crop = Math.Abs(Math.Log((double)nativeWidth / nativeHeight / targetRatio));
                double scale = Math.Max((double)width / nativeWidth, (double)height / nativeHeight);
                // Minimize cropping first, then prefer the nearest size that does not need upscaling.
                var score = (Crop: crop, Upscale: scale > 1, ScaleDistance: Math.Abs(Math.Log(scale)));
                if (score.CompareTo(bestScore) < 0)
                {
                    bestSize = (nativeWidth, nativeHeight);
                    bestScore = score;
                }
            }
        }
        return bestSize;
    }

    private static bool IsValidCustomSize(int width, int height, int maximumPixels) =>
        width > 0 && height > 0
        && width <= MaximumCustomSizeEdge && height <= MaximumCustomSizeEdge
        && width % CustomSizeStep == 0 && height % CustomSizeStep == 0
        && width <= 3 * height && height <= 3 * width
        && width * height >= MinimumCustomSizePixels && width * height <= maximumPixels;

    /// <summary>
    /// Selects the supported Imagen aspect ratio that preserves the most image area after center cropping.
    /// Requested dimensions must each be from 64 through 4096.
    /// </summary>
    public static ImageAspectRatio GetImagenAspectRatio(int width, int height)
    {
        ValidateDimensions(width, height);
        return GetClosestSize(width, height, [(1, 1), (4, 3), (3, 4), (16, 9), (9, 16)]) switch
        {
            (4, 3) => ImageAspectRatio.Landscape,
            (3, 4) => ImageAspectRatio.Portrait,
            (16, 9) => ImageAspectRatio.Widescreen,
            (9, 16) => ImageAspectRatio.PortraitTall,
            _ => ImageAspectRatio.Square
        };
    }

    private static (int Width, int Height) GetClosestSize(int width, int height, (int Width, int Height)[] sizes)
    {
        double targetRatio = (double)width / height;
        // Log-ratio distance treats portrait and landscape symmetrically and minimizes cropped image area.
        return sizes.MinBy(size => Math.Abs(Math.Log((double)size.Width / size.Height / targetRatio)));
    }

    /// <summary>Selects a Gemini aspect ratio with the least final cropping.</summary>
    public static string GetGeminiAspectRatio(int width, int height)
    {
        ValidateDimensions(width, height);
        var size = GetClosestSize(width, height, [(1, 1), (2, 3), (3, 2), (3, 4), (4, 3),
            (4, 5), (5, 4), (9, 16), (16, 9), (21, 9)]);
        return $"{size.Width}:{size.Height}";
    }

    private static string AddCompositionPrompt(string prompt, int width, int height, (int Width, int Height)? nativeSize = null)
    {
        if (nativeSize is not { } size)
        {
            return $"""
                {prompt}

                Composition target: {width}x{height} pixels ({width}:{height} aspect ratio), delivered by a centered crop.
                Keep subjects and story-critical objects inside the final safe area with generous margins for cropping.
                These are framing instructions, not text or dimension labels to draw in the image.
                """;
        }

        bool sameRatio = width * size.Height == height * size.Width;
        string resizing = width > size.Width || height > size.Height ? "upscaling" : "resizing";
        string delivery = size == (width, height)
            ? "delivered without resizing or cropping"
            : sameRatio
                ? $"delivered by proportional {resizing} without cropping"
                : $"delivered by {resizing} and a centered crop";
        string framing = sameRatio
            ? "Keep subjects and story-critical objects fully inside the frame."
            : "Keep subjects and story-critical objects inside the final safe area with generous margins for cropping.";
        return $"""
            {prompt}

            Composition target: {width}x{height} pixels ({width}:{height} aspect ratio), generated at {size.Width}x{size.Height} pixels, {delivery}.
            {framing}
            These are framing instructions, not text or dimension labels to draw in the image.
            """;
    }

    private static bool IsGPTImage(string model) => model.StartsWith("gpt-image-", StringComparison.Ordinal);

    private static bool IsSunburst(string model) =>
        model is GPTImage25SunburstModel or GPTImage25SunburstModel + GPTImage25SnapshotSuffix;

    // GPT Image 2.5 aliases and dated snapshots accept custom native sizes and xhigh quality.
    private static bool IsGPTImage25(string model) =>
        GPTImage25Models.Any(alias => model == alias || model == alias + GPTImage25SnapshotSuffix);

    private static void ValidateOpenAIPrompt(string prompt, string model)
    {
        if (IsGPTImage(model) && prompt.EnumerateRunes().Count() is var count && count > MaximumOpenAIPromptCharacters)
        {
            throw new ArgumentException($"OpenAI image prompt has {count} characters; the maximum is "
                + $"{MaximumOpenAIPromptCharacters}, including composition guidance. Shorten the brief or context. "
                + "No prompt was truncated and no provider request was submitted.", nameof(prompt));
        }
    }

    private static string GetOpenAIQuality(string model, SDKEnums.ImageQualityType quality)
    {
        if ((!IsGPTImage(model) && model is not ("dall-e-2" or "dall-e-3")) || !Enum.IsDefined(quality))
        {
            throw new NotSupportedException($"Unsupported image model or quality: {model}, {quality}.");
        }
        if (quality == SDKEnums.ImageQualityType.XHigh && !IsGPTImage25(model))
        {
            throw new NotSupportedException($"Image quality xhigh is not supported by {model}.");
        }
        return quality.ToString().ToLowerInvariant();
    }

    private static void ValidateDimensions(int width, int height)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(width, 64);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(width, 4096);
        ArgumentOutOfRangeException.ThrowIfLessThan(height, 64);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(height, 4096);
    }

    private static string CreateBoundary()
    {
        Random _random = new Random();
        char[] _boundaryValues = "0123456789=ABCDEFGHIJKLMNOPQRSTUVWXYZ_abcdefghijklmnopqrstuvwxyz".ToCharArray();
        Span<char> chars = new char[70];
        byte[] random = new byte[70];
        _random.NextBytes(random);
        int mask = 255 >> 2;
        int i = 0;
        for (; i < 70; i++)
        {
            chars[i] = _boundaryValues[random[i] & mask];
        }
        return chars.ToString();
    }
    #endregion Static Methods

}

public class BC(MultipartFormDataContent multipartForm) : BinaryContent
{
    public override void Dispose()
    {
        multipartForm.Dispose();
    }

    public override bool TryComputeLength(out long length)
    {
        if (multipartForm.Headers.ContentLength is long contentLength)
        {
            length = contentLength;
            return true;
        }
        length = 0;
        return false;
    }

    public override void WriteTo(Stream stream, CancellationToken cancellationToken = default)
    {
        multipartForm.CopyTo(stream, default, cancellationToken);
    }

    public override async Task WriteToAsync(Stream stream, CancellationToken cancellationToken = default)
    {
        await multipartForm.CopyToAsync(stream, default, cancellationToken);
    }
}