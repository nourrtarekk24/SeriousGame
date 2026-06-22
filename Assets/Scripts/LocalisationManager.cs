using UnityEngine;
using TMPro;
using System.Collections;
using System.Collections.Generic;
using ArabicSupport;

public class LocalisationManager : MonoBehaviour
{
    public static LocalisationManager Instance { get; private set; }

    private Dictionary<TextMeshProUGUI, (TMP_FontAsset font, string text)> _originals
        = new Dictionary<TextMeshProUGUI, (TMP_FontAsset, string)>();

    private static readonly Dictionary<string, string> kTranslations
        = new Dictionary<string, string>
    {
        { "Play",                           "العَب"                              },
        { "Settings",                       "الإعدادات"                          },
        { "Back",                           "رجوع"                               },
        { "Next",                           "التالي"                             },
        { "Continue",                       "استمر"                              },
        { "Start",                          "ابدأ"                               },
        { "Quit",                           "خروج"                               },
        { "Yes",                            "نعم"                                },
        { "No",                             "لا"                                 },
        { "OK",                             "حسناً"                              },
        { "Close",                          "إغلاق"                              },
        { "Retry",                          "حاول مجدداً"                        },
        { "Home",                           "الرئيسية"                           },
        { "Fruit Finder",                   "باحث الثمار"                        },
        { "Level 1",                        "المستوى ١"                          },
        { "Level 2",                        "المستوى ٢"                          },
        { "Level 3",                        "المستوى ٣"                          },
        { "Level 4",                        "المستوى ٤"                          },
        { "Level 5",                        "المستوى ٥"                          },
        { "Level 6",                        "المستوى ٦"                          },
        { "Locked",                         "مقفل"                               },
        { "Skip",                           "تخطَّ"                              },
        { "Emotion Quest",                  "رحلة المشاعر"                       },
        { "Happy",                          "سَعِيد"                             },
        { "Sad",                            "حَزِين"                             },
        { "Fear",                           "خَائِف"                             },
        { "Angry",                          "غَاضِب"                             },
        { "Surprised",                      "مُتَفَاجِئ"                         },
        { "Disgusted",                      "مُشْمَئِزّ"                         },
        { "Neutral",                        "مُحَايِد"                           },
        { "Select Level",                   "اختر المستوى"                       },
        { "Adaptive",                       "تكيفي"                              },
        { "Fixed",                          "ثابت"                               },
        { "Difficulty",                     "الصعوبة"                            },
        { "Level Complete!",                "أنجزت المستوى!"                     },
        { "Well Done!",                     "أحسنت!"                             },
        { "Score",                          "النتيجة"                            },
        { "Emotion Mirror",                 "مرآة المشاعر"                       },
        { "Upload New Photo",               "رَفْعُ صُورَةٍ جَدِيدَة"            },
        { "Upload Child's Photo",           "رَفْعُ صُورَةِ الطِّفْل"            },
        { "Check My Face!",                 "تَحَقَّقْ مِنْ وَجْهِي!"            },
        { "Make a HAPPY face!",             "اِصْنَعْ وَجْهَ السَّعَادَة!"       },
        { "Make a SAD face!",               "اِصْنَعْ وَجْهَ الحُزْن!"           },
        { "Make a SCARED face!",            "اِصْنَعْ وَجْهَ الخَوْف!"           },
        { "Make an ANGRY face!",            "اِصْنَعْ وَجْهَ الغَضَب!"           },
        { "Make a SURPRISED face!",         "اِصْنَعْ وَجْهَ الدَّهْشَة!"       },
        { "Make a DISGUSTED face!",         "اِصْنَعْ وَجْهَ الاِشْمِئْزَاز!"   },
        { "Make a NEUTRAL face.",           "اِصْنَعْ وَجْهًا مُحَايِدًا."      },
        { "FOLLOW THESE TIPS:",             "اِتَّبِعْ هَذِهِ النَّصَائِح:"     },
        { "No photo uploaded yet.",         "لَمْ تُرْفَعْ صُورَةٌ بَعْد."      },
        { "Next Emotion",                   "الشُّعُورُ التَّالِي"               },
        { "Result",                         "النتيجة"                            },
        { "Language",                       "اللغة"                              },
        { "Music",                          "الموسيقى"                           },
        { "Voice",                          "الصوت"                              },
        { "Backgrounds",                    "خلفية"                              },
        { "Background",                     "خلفية"                              },
        { "Backgrounds ON",                 "خلفية"                              },
        { "Backgrounds OFF",                "خلفية"                              },
        { "Scene Backgrounds",              "خلفية"                              },
        { "scene background",               "خلفية"                              },
        { "ON",                             "تشغيل"                              },
        { "OFF",                            "إيقاف"                              },

        { "How is this friend feeling?",    "كَيْفَ يَشْعُرُ هَذَا الصَّدِيق؟"  },
        { "This friend is feeling...",      "هَذَا الصَّدِيقُ يَشْعُر..."       },
        { "Choose your answer",             "اِخْتَرْ إِجَابَتَك"               },

        { "Choose an emotion to imitate",   "اِخْتَرْ مَشَاعِرًا لِتُقَلِّدَهَا" },
        { "The child will try to make this face.", "سَيُحَاوِلُ الطِّفْلُ صُنْعَ هَذَا الوَجْه." },
        { "Analyse",                        "تَحْلِيل"                           },
        { "Try Again",                      "حَاوِلْ مُجَدَّدًا"                 },

        { "Select a Level",                 "اِخْتَرْ مُسْتَوًى"                 },
        { "Difficulty: Adaptive",           "الصعوبة: تكيفية"                    },
        { "Difficulty: Fixed",              "الصعوبة: ثابتة"                     },

        { "Check Progress",                 "تَحَقَّقْ مِنَ التَّقَدُّم"         },
        { "Play Again",                     "العَبْ مُجَدَّدًا"                  },
        { "Menu",                           "القائمة"                            },
    };

    void Awake() { Instance = this; }

    void Start()
    {
        if (!GameManager.IsArabic()) return;
        TranslateAll();
        StartCoroutine(DelayedTranslate());
    }

    IEnumerator DelayedTranslate()
    {
        yield return new WaitForSeconds(0.5f);
        TranslateAll();
    }

    public void TranslateAll()
    {
        if (!GameManager.IsArabic()) return;
        var allTMPs = FindObjectsOfType<TextMeshProUGUI>(true);
        int count = 0;

        foreach (var tmp in allTMPs)
        {
            if (tmp == null) continue;
            string original = tmp.text.Trim();
            if (string.IsNullOrEmpty(original)) continue;

            if (!_originals.ContainsKey(tmp))
                _originals[tmp] = (tmp.font, original);

            if (kTranslations.TryGetValue(original, out string ar))
            {
                tmp.text = PrepareArabic(tmp, ar);
                count++;
                continue;
            }

            foreach (var kvp in kTranslations)
            {
                if (original.Contains(kvp.Key))
                {
                    tmp.text = PrepareArabic(tmp, kvp.Value);
                    count++;
                    break;
                }
            }
        }

        Debug.Log($"[Localisation] Translated {count} components.");
    }

    string PrepareArabic(TextMeshProUGUI tmp, string arabic)
    {
        if (GameManager.Instance?.arabicFallbackFont != null)
            tmp.font = GameManager.Instance.arabicFallbackFont;
        if (string.IsNullOrEmpty(arabic)) return arabic;
        if (!arabic.Contains("\n"))
            return ArabicFixer.Fix(arabic);
        string[] lines = arabic.Split('\n');
        for (int i = 0; i < lines.Length; i++)
            lines[i] = ArabicFixer.Fix(lines[i]);
        return string.Join("\n", lines);
    }

    public static string ShapeArabic(string arabic)
    {
        if (string.IsNullOrEmpty(arabic)) return arabic;
        return ArabicFixer.Fix(arabic);
    }

    public void RestoreAll()
    {
        foreach (var kvp in _originals)
        {
            if (kvp.Key == null) continue;
            kvp.Key.font = kvp.Value.font;
            kvp.Key.text = kvp.Value.text;
        }
        _originals.Clear();
    }
}