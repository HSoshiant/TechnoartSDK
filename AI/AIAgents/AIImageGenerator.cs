using System.ClientModel;
using System.ComponentModel;
using System.Net.Http.Headers;
using Microsoft.Extensions.Logging;
using Mscc.GenerativeAI;
using OpenAI.Images;
using TechnoartSDK.Extensions;
using TechnoartSDK.Models;

namespace TechnoartSDK.AI.AIAgents;

public class AIImageGenerator(ILogger<AIImageGenerator> logger)
{
    public async Task<ImageModel?> CreateImagen(string name, string text2ImagePrompt, ImageAspectRatio aspectRatio)
    {
        logger.LogInformation("Starting image generation for character: {Name}", name);

        var googleAI = new GoogleAI(apiKey: AIServicesExtensions.GoogleApiKey);
        //var vertextAI = new VertexAI(apiKey: _APIKEY);

        var vModel = googleAI.GenerativeModel(model: Mscc.GenerativeAI.Model.Imagen3);
        var res = await vModel.GenerateImages(text2ImagePrompt, aspectRatio: aspectRatio, personGeneration: PersonGeneration.AllowAll);
        try
        {
            var img = res.Images.FirstOrDefault();
            return img != null ? new ImageModel { Data = img.BytesBase64Encoded!, MimeType = img.MimeType! } : null;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, $"Error in creating character \"{name}\": {ex.Message}");
        }
        return null;

    }

    public async Task<string?> CreateGeminiImage(string name, string text2ImagePrompt, string aspectRatio, params string[] imagesData)
    {
        logger.LogInformation("Starting Gemini image generation for: {Name}", name);
        var googleAI = new GoogleAI(apiKey: AIServicesExtensions.GoogleApiKey);
        var vModel = googleAI.GenerativeModel(model: Mscc.GenerativeAI.Model.Gemini20FlashImageGeneration);

        var parts = new List<IPart>(){
            new TextData
            {
                Text = @$"DRAW :
{text2ImagePrompt}
" } };
        if (imagesData is not null && imagesData.Length > 0)
        {
            parts.AddRange(imagesData.Select(img => new InlineData
            {
                MimeType = "image/png",
                Data = img
            }));
        }
        var res = await vModel.GenerateContent(
            parts
            , new()
            {
                ResponseModalities = [Mscc.GenerativeAI.ResponseModality.Image, Mscc.GenerativeAI.ResponseModality.Text],
            });
        try
        {
            var imgData = res.Candidates!.First().
                Content!.Parts.First(p => p.InlineData is not null)
                .InlineData.Data;

            return imgData;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, $"Error in creating Gemini image for \"{name}\": {ex.Message}");
            return null;
        }
    }

    #region OpenAI Image Generation
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
    }

    public async Task<ImageModel?> CreateOpenAIImage(string name, string text2ImagePrompt, OpenAIImageModel model, SDKEnums.ImageQualityType quality)
    {
        try
        {
            var modelName = model.ToDescriptionString();
            ImageClient client = new(modelName, AIServicesExtensions.OpenApiKey);

            logger.LogInformation("Starting OpenAI image generation for: {Name}", name);
            ImageGenerationOptions op = new()
            {
                Quality = model == OpenAIImageModel.GPTImage1 || model == OpenAIImageModel.GPTImage15
                    ? new GeneratedImageQuality(quality.ToString().ToLower())
                    : new GeneratedImageQuality("hd"),

                Size = model == OpenAIImageModel.GPTImage1 || model == OpenAIImageModel.GPTImage15
                    ? new GeneratedImageSize(1536, 1024)
                    : new GeneratedImageSize(1792, 1024)
            };

            var res = await client.GenerateImageAsync(text2ImagePrompt, op);

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
                    return await client.GetByteArrayAsync(url);
                }
                imgModel = new ImageModel
                {
                    Data = Convert.ToBase64String(await DownloadAsync(res.Value.ImageUri)),
                    MimeType = "image/png"
                };
            }

            //File.WriteAllBytes("result.png", img.ImageBytes);
            return imgModel;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, ex.Message);
        }
        return null;
    }

    public async Task<ImageModel?> CreateOpenAIImageByRefrence(string name, string text2ImagePrompt, OpenAIImageModel model, SDKEnums.ImageQualityType quality, params (string Name, ImageModel Image)[] imagesData)
    {
        try
        {
            ImageClient client;
            MultipartFormDataContent content = new(CreateBoundary());
            var modelName = model.ToDescriptionString();
            client = new(modelName, AIServicesExtensions.OpenApiKey);
            content.Add(new StringContent(modelName), "model");

            content.Add(model == OpenAIImageModel.GPTImage1 || model == OpenAIImageModel.GPTImage15
                ? new StringContent("1536x1024")
                : new StringContent("1792x1024")
                , "size");
            if (model == OpenAIImageModel.GPTImage1 || model == OpenAIImageModel.GPTImage15)
            {
                content.Add(new StringContent(quality.ToString().ToLower()), "quality");
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

            logger.LogInformation("Starting OpenAI image generation for: {Name}", name);
            //var res = c.GenerateImageEdit(name, text2ImagePrompt);
            var result = await client.GenerateImageEditsAsync(bc, content.Headers.ContentType.ToString());
#pragma warning disable OPENAI001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
            OpenAI.Images.GeneratedImage img = ((GeneratedImageCollection) result).FirstOrDefault();
#pragma warning restore OPENAI001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
            var imgModel = new ImageModel
            {
                Data = Convert.ToBase64String(img.ImageBytes),
                MimeType = "image/png"
            };
            //File.WriteAllBytes("result.png", img.ImageBytes);
            return imgModel;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, ex.Message);
        }
        return null;
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
    #endregion OpenAI Image Generation

}

public class BC(MultipartFormDataContent multipartForm) :BinaryContent
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