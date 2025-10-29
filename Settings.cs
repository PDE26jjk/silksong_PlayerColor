using BepInEx;
using BepInEx.Configuration;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEngine;
namespace silksong_PlayerColor {
    public class Settings {

        #region Configuration Properties
        private static Dictionary<string, Dictionary<string, string>> Sections = new() {
            ["Global"] = new(){
                { "EN", "0 Global" },
                { "ZH", "0 全局" }
            },
        };

        #region Global
        [Section("Global")]
        [Description("EN", "Change Cloak Color", "Change color of cloak or not")]
        [Description("ZH", "更改斗篷颜色", "是否更改斗篷颜色")]
        [DefaultValue(true)]
        public static ConfigEntry<bool> UseOverrideMaterial { get; private set; }

        [Section("Global")]
        [Description("EN", "Brightness", "Brightness of cloak")]
        [Description("ZH", "亮度", "斗篷的亮度")]
        [DefaultValue(0.75f)]
        [Range(0f, 2f)]
        public static ConfigEntry<float> Brightness { get; private set; }

        [Section("Global")]
        [Description("EN", "Saturation", "Saturation of cloak")]
        [Description("ZH", "饱和度", "斗篷的饱和度")]
        [DefaultValue(1.6f)]
        [Range(0f, 2f)]
        public static ConfigEntry<float> Saturation { get; private set; }

        //[Section("Global")]
        //[Description("EN", "Origin Color", "Origin color of cloak")]
        //[Description("ZH", "原始颜色", "斗篷的原始颜色")]
        //[DefaultValue("#79404B")]
        //public static ConfigEntry<Color> Color1 { get; private set; }
  
        [Section("Global")]
        [Description("EN", "Target Color", "Target color of cloak")]
        [Description("ZH", "目标颜色", "斗篷的目标颜色")]
        [DefaultValue("#66ccff")]
        public static ConfigEntry<Color> Color2 { get; private set; }

        [Section("Global")]
        [Description("EN", "Load all textures at startup", "Whether to load all textures at startup or load them when needed")]
        [Description("ZH", "启动时加载所有纹理", "是否启动时加载所有纹理，否则在用到时加载")]
        [DefaultValue(true)]
        public static ConfigEntry<bool> LoadAllTextureAtStart { get; private set; }
        #endregion


        #endregion

        public static void Initialize(ConfigFile config, string lang) {
            // 获取所有配置属性
            var configProperties = typeof(Settings)
                .GetProperties(BindingFlags.Public | BindingFlags.Static)
                .Where(p => p.PropertyType.IsGenericType &&
                            p.PropertyType.GetGenericTypeDefinition() == typeof(ConfigEntry<>))
                .ToList();
            int order = configProperties.Count;
            foreach (var property in configProperties) {
                order--;
                // 获取配置属性
                var sectionAttr = property.GetCustomAttribute<SectionAttribute>();
                var descAttr = property.GetCustomAttributes<DescriptionAttribute>().ToList();
                var defaultValueAttr = property.GetCustomAttribute<DefaultValueAttribute>();
                var rangeAttr = property.GetCustomAttribute<RangeAttribute>();

                // 获取分类名称（本地化）
                string sectionName = sectionAttr?.Name ?? "General";
                string localizedSection = GetLocalizedSection(sectionName, lang);

                // 确定当前语言描述
                string keyName = property.Name;
                string description = "No description";

                // 根据系统语言选择描述
                var currentLang = Application.systemLanguage;
                var langDesc = descAttr.FirstOrDefault(d =>
                    d.Language == lang);

                langDesc ??= descAttr.FirstOrDefault(d =>
                    d.Language == "EN");

                if (langDesc != null) {
                    keyName = langDesc.KeyName;
                    description = langDesc.Description;
                }
                else if (descAttr.Count > 0) {
                    // 默认使用第一个描述
                    keyName = descAttr[0].KeyName;
                    description = descAttr[0].Description;
                }
                var propertyType = property.PropertyType.GetGenericArguments()[0];
                // 获取默认值
                object defaultValue = defaultValueAttr?.Value;
                if (defaultValue == null) {
                    // 尝试获取属性类型的默认值
                    defaultValue = propertyType.IsValueType ? Activator.CreateInstance(propertyType) : null;
                }

                //Debug.Log("key------"+keyName);
                // 创建配置描述
                ConfigDescription configDesc;
                if (rangeAttr != null) {
                    configDesc = new ConfigDescription(
                        description,
                        new AcceptableValueRange<float>(rangeAttr.Min, rangeAttr.Max),
                        new ConfigurationManagerAttributes { Order = order }
                    );
                }
                else {
                    configDesc = new ConfigDescription(description, null, new ConfigurationManagerAttributes { Order = order });
                }

                defaultValue = ProcessSpecialTypes(defaultValue, propertyType);
                // 创建配置项
                // 获取配置项的实际类型
                Type configType = property.PropertyType.GetGenericArguments()[0];

                // 使用反射创建配置项
                var bindMethod = typeof(ConfigFile).GetMethods()
                    .Where(m => m.Name == "Bind" && m.IsGenericMethod)
                    .Where(m => {
                        var parameters = m.GetParameters();
                        return parameters.Length == 4 &&
                               parameters[0].ParameterType == typeof(string) &&
                               parameters[1].ParameterType == typeof(string) &&
                               parameters[3].ParameterType == typeof(ConfigDescription);
                    })
                    .FirstOrDefault().MakeGenericMethod(configType);


                // 准备参数
                object[] parameters = new object[] {
                    localizedSection, // section
                    keyName,           // key
                    defaultValue,      // defaultValue
                    configDesc         // configDescription
                };

                // 调用Bind方法
                object configEntry = bindMethod.Invoke(config, parameters);

                // 设置属性值
                property.SetValue(null, configEntry);
            }
        }

        private static string GetLocalizedSection(string sectionName, string langCode) {
            if (Sections.TryGetValue(sectionName, out var translations)) {
                if (translations.TryGetValue(langCode, out string localized)) {
                    return localized;
                }

                // 尝试英语作为备选
                if (translations.TryGetValue("EN", out localized)) {
                    return localized;
                }
            }

            // 返回原始名称
            return sectionName;
        }
        private static object ProcessSpecialTypes(object defaultValue, Type configType) {
            // 处理键盘快捷键类型
            if (configType == typeof(KeyboardShortcut)) {
                return ConvertToKeyboardShortcut(defaultValue);
            }

            // 处理颜色类型
            if (configType == typeof(Color)) {
                return ConvertToColor(defaultValue);
            }
            return defaultValue;
        }
        private static object ConvertToColor(object value) {
            // 如果已经是Color类型，直接返回
            if (value is Color color) {
                return color;
            }

            // 如果值是字符串，尝试解析为颜色
            if (value is string colorString) {
                return ParseColor(colorString);
            }

            // 默认返回白色
            return Color.white;
        }
        public static Color ParseColor(string colorString) {
            // 尝试解析为颜色名称
            if (ColorUtility.TryParseHtmlString(colorString, out Color color)) {
                return color;
            }

            // 尝试常见颜色名称
            switch (colorString.ToLower()) {
                case "red": return Color.red;
                case "green": return Color.green;
                case "blue": return Color.blue;
                case "yellow": return Color.yellow;
                case "cyan": return Color.cyan;
                case "magenta": return Color.magenta;
                case "white": return Color.white;
                case "black": return Color.black;
                case "gray": return Color.gray;
            }

            Debug.LogError($"Invalid color value: {colorString}");
            return Color.white;
        }
        private static object ConvertToKeyboardShortcut(object value) {
            // 如果已经是KeyboardShortcut类型，直接返回
            if (value is KeyboardShortcut shortcut) {
                return shortcut;
            }

            // 如果值是KeyCode类型，转换为KeyboardShortcut
            if (value is KeyCode keyCode) {
                return new KeyboardShortcut(keyCode);
            }

            // 如果值是字符串，尝试解析为KeyCode
            if (value is string keyString) {
                if (Enum.TryParse<KeyCode>(keyString, out KeyCode parsedKey)) {
                    return new KeyboardShortcut(parsedKey);
                }
            }

            // 默认返回空快捷键
            return new KeyboardShortcut(KeyCode.None);
        }
        private static Color HexToColor(string hex) {
            if (ColorUtility.TryParseHtmlString(hex, out Color color)) {
                return color;
            }
            Debug.LogError($"Invalid color code: {hex}");
            return Color.white;
        }

        #region Annotations

        /// 
        /// Configuration item category
        /// 
        [AttributeUsage(AttributeTargets.Property)]
        public class SectionAttribute : Attribute {
            public string Name { get; }

            public SectionAttribute(string name) {
                Name = name;
            }
        }

        /// 
        /// Multilingual description
        /// 
        [AttributeUsage(AttributeTargets.Property, AllowMultiple = true)]
        public class DescriptionAttribute : Attribute {
            public string Language { get; }
            public string KeyName { get; }
            public string Description { get; }

            public DescriptionAttribute(string language, string keyName, string description) {
                Language = language;
                KeyName = keyName;
                Description = description;
            }
        }

        /// 
        /// Default value
        /// 
        [AttributeUsage(AttributeTargets.Property)]
        public class DefaultValueAttribute : Attribute {
            public object Value { get; }

            public DefaultValueAttribute(object value) {
                Value = value;
            }

            // Overloads to support various types
            public DefaultValueAttribute(string value) : this((object)value) { }
            public DefaultValueAttribute(float value) : this((object)value) { }
            public DefaultValueAttribute(int value) : this((object)value) { }
            public DefaultValueAttribute(bool value) : this((object)value) { }
            public DefaultValueAttribute(KeyCode value) : this((object)value) { }
        }

        /// 
        /// Value range restriction
        /// 
        [AttributeUsage(AttributeTargets.Property)]
        public class RangeAttribute : Attribute {
            public float Min { get; }
            public float Max { get; }

            public RangeAttribute(float min, float max) {
                Min = min;
                Max = max;
            }
        }
        #endregion
    }
}