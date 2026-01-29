using Microsoft.Extensions.Logging;
using STranslate.Plugin;
using STranslate.ViewModels;
using System.IO;
using System.Net;
using System.Text;
using System.Text.Json;

namespace STranslate.Core;

public class ExternalCallService(
    ILogger<ExternalCallService> logger,
    MainWindowViewModel viewModel,
    Internationalization i18n,
    INotification notification)
{
    private HttpListener? _listener;

    public bool IsStarted { get; private set; }

    public bool StartService(string prefix)
    {
        StopService();

        try
        {
            _listener = new HttpListener();
            _listener.Prefixes.Add(prefix);

            _listener.Start();
            _listener.BeginGetContext(Callback, _listener);
            IsStarted = true;

            OnActionOccurred?.Invoke(string.Empty);
            return true;
        }
        catch (Exception ex)
        {
            var msg = $"启动服务失败请重新配置端口: {prefix}";
            logger.LogError(ex, msg);
            OnActionOccurred?.Invoke(msg);
            notification.Show(i18n.GetTranslation("Prompt"), msg);

            return false;
        }
    }

    public void StopService()
    {
        if (!IsStarted)
            return;

        OnActionOccurred?.Invoke(string.Empty);
        _listener?.Close();
        _listener = null;
        IsStarted = false;
    }

    private void Callback(IAsyncResult ar)
    {
        if (!IsStarted || _listener == null || !_listener.IsListening)
            return;

        HttpListenerContext context;
        try
        {
            context = _listener.EndGetContext(ar);
        }
        catch (Exception)
        {
            // HttpListener has been disposed, no need to handle the request
            return;
        }

        _listener.BeginGetContext(Callback, _listener);

        try
        {
            var request = context.Request;

            // Get the URL from the request
            var uri = request.Url ?? throw new Exception("get url is null");

            if (uri.Segments.Length > 2)
                throw new Exception("path does not meet the requirements");

            // Get the path from the URL
            var path = uri.AbsolutePath.TrimStart('/');
            path = path == "" ? "translate" : path;

            // Get the external call action based on the path
            var ecAction = GetExternalCallAction(path);

            // Read the content of the request
            using var reader = new StreamReader(request.InputStream, request.ContentEncoding);
            var content = reader.ReadToEnd();

            //Please use GET like `curl localhost:50020/translate -d \"hello world\"`"
            switch (request.HttpMethod)
            {
                case "GET":
                    ExecuteExternalCall(ecAction, "");
                    break;

                case "POST":
                    ExecuteExternalCall(ecAction, content);
                    break;

                default:
                    throw new Exception("Method Not Allowed");
            }

            ResponseHandler(context.Response);
        }
        catch (Exception e)
        {
            ResponseHandler(context.Response, e.Message);
            logger.LogError(e, $"ExternalCall Error, {e.Message}");
        }
    }

    private void ExecuteExternalCall(ExternalCallAction action, string content)
    {
        App.Current.Dispatcher.Invoke(async () =>
        {
            switch (action)
            {
                case ExternalCallAction.translate:
                    if (string.IsNullOrWhiteSpace(content))
                        viewModel.InputClearCommand.Execute(null);
                    else
                        viewModel.ExecuteTranslate(content);
                    break;
                case ExternalCallAction.translate_force:
                    if (string.IsNullOrWhiteSpace(content))
                        viewModel.InputClearCommand.Execute(null);
                    else
                        viewModel.ExecuteTranslate(content, "force");
                    break;
                case ExternalCallAction.translate_input:
                    viewModel.InputClearCommand.Execute(null);
                    break;
                case ExternalCallAction.translate_ocr:
                    if (string.IsNullOrWhiteSpace(content))
                        viewModel.ScreenshotTranslateCommand.Execute(null);
                    else
                    {
                        using var bitmap = Utilities.ToBitmap(content);
                        await viewModel.ScreenshotTranslateHandlerAsync(bitmap);
                    }
                    break;
                case ExternalCallAction.translate_ocr_image:
                    if (string.IsNullOrWhiteSpace(content))
                        viewModel.ImageTranslateCommand.Execute(null);
                    else
                    {
                        using var bitmap = Utilities.ToBitmap(content);
                        await viewModel.ImageTranslateHandlerAsync(bitmap);
                    }
                    break;
                case ExternalCallAction.translate_crossword:
                    viewModel.CrosswordTranslateCommand.Execute(null);
                    break;
                case ExternalCallAction.translate_mousehook:
                    viewModel.ToggleMouseHookTranslateCommand.Execute(null);
                    break;
                case ExternalCallAction.translate_replace:
                    {
                        if (viewModel.ReplaceTranslateCommand.IsRunning)
                        {
                            viewModel.ReplaceTranslateCancelCommand.Execute(null);
                            return;
                        }
                        viewModel.ReplaceTranslateCommand.Execute(null);
                    }
                    break;
                case ExternalCallAction.ocr:
                    if (string.IsNullOrWhiteSpace(content))
                        viewModel.OcrCommand.Execute(null);
                    else
                    {
                        using var bitmap = Utilities.ToBitmap(content);
                        await viewModel.OcrHandlerAsync(bitmap);
                    }
                    break;
                case ExternalCallAction.ocr_silence:
                    if (string.IsNullOrWhiteSpace(content))
                    {
                        if (viewModel.SilentOcrCommand.IsRunning)
                        {
                            viewModel.SilentOcrCancelCommand.Execute(null);
                            return;
                        }
                        viewModel.SilentOcrCommand.Execute(null);
                    }
                    else
                    {
                        using var bitmap = Utilities.ToBitmap(content);
                        await viewModel.SilentOcrHandlerAsync(bitmap);
                    }
                    break;
                case ExternalCallAction.ocr_qrcode:
                    if (string.IsNullOrWhiteSpace(content))
                        viewModel.QrCodeCommand.Execute(null);
                    else
                    {
                        using var bitmap = Utilities.ToBitmap(content);
                        await viewModel.QrCodeHandlerAsync(bitmap);
                    }
                    break;
                case ExternalCallAction.open_window:
                    viewModel.ToggleAppCommand.Execute(null);
                    break;
                case ExternalCallAction.open_preference:
                    viewModel.OpenSettingsCommand.Execute(null);
                    break;
                case ExternalCallAction.open_history:
                    viewModel.OpenHistoryCommand.Execute(null);
                    break;
                case ExternalCallAction.forbiddenhotkey:
                    viewModel.ToggleGlobalHotkey();
                    break;
                case ExternalCallAction.tts_silence:
                    if (string.IsNullOrWhiteSpace(content))
                    {
                        if (viewModel.SilentTtsCommand.IsRunning)
                        {
                            viewModel.SilentTtsCancelCommand.Execute(null);
                            return;
                        }
                        viewModel.SilentTtsCommand.Execute(null);
                    }
                    else
                        await viewModel.SilentTtsHandlerAsync(content);
                    break;
                default:
                    break;
            }
        });
    }

    /// <summary>
    ///     字符串=>外部调用枚举
    /// </summary>
    /// <param name="source"></param>
    /// <returns></returns>
    private ExternalCallAction GetExternalCallAction(string source)
    {
        return Enum.TryParse<ExternalCallAction>(source, out var eAction)
            ? eAction
            : throw new Exception("path does not meet the requirements");
    }

    private void ResponseHandler(HttpListenerResponse response, string? error = null)
    {
        response.StatusCode = HttpStatusCode.OK.GetHashCode();
        response.ContentType = "application/json;charset=UTF-8";
        response.ContentEncoding = Encoding.UTF8;
        response.AppendHeader("Content-Type", "application/json;charset=UTF-8");

        var data = new
        {
            code = error is null ? HttpStatusCode.OK : HttpStatusCode.InternalServerError,
            data = error ?? "Call Succeed"
        };

        using StreamWriter writer = new(response.OutputStream, Encoding.UTF8);
        writer.Write(JsonSerializer.Serialize(data));
        writer.Close();
        response.Close();
    }

    public Action<string>? OnActionOccurred;
}

/// <summary>
///     外部调用功能
/// </summary>
public enum ExternalCallAction
{
    translate = 1,
    translate_force,
    translate_input,
    translate_ocr,
    translate_ocr_image,
    translate_crossword,
    translate_mousehook,
    translate_replace,
    ocr,
    ocr_silence,
    ocr_qrcode,
    open_window,
    open_preference,
    open_history,
    forbiddenhotkey,
    tts_silence
}