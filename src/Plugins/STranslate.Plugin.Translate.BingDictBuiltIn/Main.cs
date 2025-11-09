using STranslate.Plugin.Translate.BingDictBuiltIn.View;
using STranslate.Plugin.Translate.BingDictBuiltIn.ViewModel;
using System.Collections.ObjectModel;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Windows.Controls;

namespace STranslate.Plugin.Translate.BingDictBuiltIn;

public class Main : DictionaryPluginBase
{
    private const string URL = "https://cn.bing.com/api/v6/dictionarywords/search";

    private Control? _settingUi;
    private SettingsViewModel? _viewModel;
    private Settings Settings { get; set; } = null!;
    private IPluginContext Context { get; set; } = null!;

    public override Control GetSettingUI()
    {
        _viewModel ??= new SettingsViewModel(Context, Settings);
        _settingUi ??= new SettingsView { DataContext = _viewModel };
        return _settingUi;
    }

    public override void Init(IPluginContext context)
    {
        Context = context;
        Settings = context.LoadSettingStorage<Settings>();
    }

    public override void Dispose() { }

    public override async Task TranslateAsync(string content, DictionaryResult result, CancellationToken cancellationToken = default)
    {
        var option = new Options
        {
            QueryParams = new Dictionary<string, string>
            {
                { "q", content.ToLower() },
                { "appid", "371E7B2AF0F9B84EC491D731DF90A55719C7D209" },
                { "mkt", "zh-cn" },
                { "pname", "bingdict" }
            }
        };

        var response = await Context.HttpService.GetAsync(URL, option, cancellationToken);

        var jsonDoc = JsonDocument.Parse(response);
        var root = jsonDoc.RootElement;

        if (!root.TryGetProperty("value", out var value) || value.ValueKind != JsonValueKind.Array || value.GetArrayLength() == 0)
        {
            result.ResultType = DictionaryResultType.NoResult;
            return;
        }

        var firstValue = value[0];

        if (!firstValue.TryGetProperty("name", out var name) ||
            name.GetString() is not string word ||
            string.IsNullOrEmpty(word) ||
            !firstValue.TryGetProperty("meaningGroups", out var meaningGroups) ||
            meaningGroups.GetArrayLength() == 0)
        {
            result.ResultType = DictionaryResultType.NoResult;
            return;
        }

        result.Text = word;
        result.ResultType = DictionaryResultType.Success;

        // 提取通用音频URL
        var audioUrl = firstValue.TryGetProperty("pronunciationAudio", out var audio) && audio.TryGetProperty("contentUrl", out var url)
            ? url.GetString() ?? string.Empty
            : string.Empty;

        // 提取释义、音标和例句
        foreach (var group in meaningGroups.EnumerateArray())
        {
            if (!group.TryGetProperty("partsOfSpeech", out var partsOfSpeech) || partsOfSpeech.GetArrayLength() == 0)
                continue;

            var firstPartOfSpeech = partsOfSpeech[0];
            var posName = firstPartOfSpeech.TryGetProperty("name", out var posNameElement) ? posNameElement.GetString() : string.Empty;
            var posDesc = firstPartOfSpeech.TryGetProperty("description", out var posDescElement) ? posDescElement.GetString() : string.Empty;

            // 提取音标
            if (posDesc?.Equals("发音") == true && !string.IsNullOrEmpty(posName))
            {
                if (group.TryGetProperty("meanings", out var meanings) && meanings.GetArrayLength() > 0 &&
                    meanings[0].TryGetProperty("richDefinitions", out var richDefs) && richDefs.GetArrayLength() > 0 &&
                    richDefs[0].TryGetProperty("fragments", out var fragments) && fragments.GetArrayLength() > 0 &&
                    fragments[0].TryGetProperty("text", out var phoneticText))
                {
                    var label = Util.IsChinese(word) ? "zh" :
                                posName.Equals("UK", StringComparison.OrdinalIgnoreCase) ? "uk" :
                                posName.Equals("US", StringComparison.OrdinalIgnoreCase) ? "us" :
                                string.Empty;

                    if (!string.IsNullOrEmpty(label))
                    {
                        result.Symbols.Add(new Symbol
                        {
                            Label = label,
                            Phonetic = phoneticText.GetString() ?? string.Empty,
                            //AudioUrl = audioUrl
                        });
                    }
                }
                continue; // 处理完音标后跳过，避免作为释义
            }

            // 提取例句
            if (Settings.EnableSentenceExample && posDesc == "例句")
            {
                ProcessSentences(group, result);
                continue;
            }

            // 提取变形
            if (posName == "变形")
            {
                ProcessInflections(group, result);
                continue;
            }

            // 过滤掉非释义内容
            string[] skipDescriptions = ["分类词典", "词组", "权威英汉双解", "权威英汉双解发音"];
            if (skipDescriptions.Contains(posDesc))
            {
                continue;
            }

            // 提取释义
            var meansList = new List<string>();
            if (group.TryGetProperty("meanings", out var meaningArray))
            {
                foreach (var meaning in meaningArray.EnumerateArray())
                {
                    if (meaning.TryGetProperty("richDefinitions", out var richDefs))
                    {
                        foreach (var def in richDefs.EnumerateArray())
                        {
                            if (def.TryGetProperty("fragments", out var fragments))
                            {
                                foreach (var fragment in fragments.EnumerateArray())
                                {
                                    if (fragment.TryGetProperty("text", out var text))
                                    {
                                        var meanText = text.GetString();
                                        if (!string.IsNullOrEmpty(meanText))
                                        {
                                            meansList.Add(meanText);
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }

            var convertPosName = ConvertPartOfSpeech(posName ?? string.Empty);
            if (meansList.Count > 0 && !string.IsNullOrEmpty(convertPosName))
            {
                var existMean = result.DictMeans.FirstOrDefault(d => d.PartOfSpeech == convertPosName);
                if (existMean == null)
                {
                    result.DictMeans.Add(new DictMean
                    {
                        PartOfSpeech = convertPosName,
                        Means = new ObservableCollection<string>(meansList)
                    });
                }
            }
        }
    }

    /// <summary>
    /// 处理例句
    /// </summary>
    /// <param name="sentenceGroup">包含例句的JsonElement</param>
    /// <param name="result">字典结果</param>
    private void ProcessSentences(JsonElement sentenceGroup, DictionaryResult result)
    {
        if (!sentenceGroup.TryGetProperty("meanings", out var meanings) || meanings.GetArrayLength() == 0)
            return;

        // 例句通常在第一个meaning中
        var firstMeaning = meanings[0];
        if (!firstMeaning.TryGetProperty("richDefinitions", out var richDefinitions))
            return;

        foreach (var def in richDefinitions.EnumerateArray())
        {
            if (!def.TryGetProperty("examples", out var examples) || examples.ValueKind != JsonValueKind.Array)
                continue;

            var sentencePair = new List<string>();
            foreach (var example in examples.EnumerateArray())
            {
                var line = example.GetString();
                if (string.IsNullOrEmpty(line)) continue;

                // 过滤掉GUID
                if (Guid.TryParse(line, out _)) continue;

                // 使用多步清理
                var cleanedLine = line;

                // 先处理所有{数字#内容$数字}格式
                cleanedLine = Regex.Replace(cleanedLine, @"{\d+#(.*?)\$\d+}", "$1");

                // 处理特殊的嵌套格式 {#*{14#中文$14}*$} 和 {##*{14#Chinese$14}*$$}
                cleanedLine = Regex.Replace(cleanedLine, @"{#*\*?\{\d+#(.*?)\$\d+\}\*\$+}", "$1");

                // 清理任何剩余的大括号结构
                cleanedLine = Regex.Replace(cleanedLine, @"{[^}]*}", "");

                sentencePair.Add(cleanedLine);

                // 每两行为一组（原文和译文）
                if (sentencePair.Count == 2)
                {
                    if (result.Sentences.Count >= 2) continue;
                    result.Sentences.Add($"{sentencePair[0]}\n{sentencePair[1]}");
                    sentencePair.Clear();
                }
            }
        }
    }

    /// <summary>
    /// 处理变形
    /// </summary>
    /// <param name="inflectionGroup"></param>
    /// <param name="result"></param>
    private void ProcessInflections(JsonElement inflectionGroup, DictionaryResult result)
    {
        if (!inflectionGroup.TryGetProperty("meanings", out var meanings) || meanings.GetArrayLength() == 0 ||
            !meanings[0].TryGetProperty("richDefinitions", out var richDefinitions) || richDefinitions.GetArrayLength() == 0 ||
            !richDefinitions[0].TryGetProperty("fragments", out var fragments))
            return;

        foreach (var fragment in fragments.EnumerateArray())
        {
            if (!fragment.TryGetProperty("text", out var textEle) || textEle.GetString() is not string text) continue;

            var parts = text.Split('：');
            if (parts.Length != 2) continue;

            var key = parts[0];
            var value = parts[1];

            switch (key)
            {
                case "复数":
                    result.Plurals.Add(value);
                    break;
                case "过去式":
                    result.PastTense.Add(value);
                    break;
                case "过去分词":
                    result.PastParticiple.Add(value);
                    break;
                case "现在分词":
                    result.PresentParticiple.Add(value);
                    break;
                case "一般现在时": // 通常是第三人称单数
                    result.ThirdPersonSingular.Add(value);
                    break;
                case "比较级":
                    result.Comparative.Add(value);
                    break;
                case "最高级":
                    result.Superlative.Add(value);
                    break;
            }
        }
    }

    public string ConvertPartOfSpeech(string value)
    {
        return value switch
        {
            "形容词" => "adj.",
            "副词" => "adv.",
            "动词" => "v.",
            "系动词" => "linkv.",
            "助动词" => "auxv.",
            "情态动词" => "modalv.",
            "名词" => "n.",
            "代词" => "pron.",
            "介词" => "prep.",
            "连词" => "conj.",
            "感叹词" => "int.",
            "限定词" => "det.",
            "冠词" => "art.",
            "缩写" => "abbr.",
            "不定词" => "inf.",
            "分词" => "part.",
            "数词" => "num.",
            "网络" => "Web",
            _ => value
        };
    }
}