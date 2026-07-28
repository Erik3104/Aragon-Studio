using Autodesk.Revit.ApplicationServices;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Events;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Events;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace AragonStudio.Services.Guardado
{
    public static class RecordatorioService
    {
        private static UIApplication _uiApp;
        private static string _configFolder;
        private static Dictionary<string, DateTime> _ultimoGuardado = new();
        private static Dictionary<string, bool> _guardadoPendiente = new();
        private static Dictionary<string, int> _frecuenciaDocs = new();
        private static Dictionary<string, bool> _activoDocs = new();
        private static Dictionary<string, DateTime> _tiempoPendiente = new();

        public static void Inicializar(UIControlledApplication app)
        {
            _configFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "AragonStudio", "Guardado");
            Directory.CreateDirectory(_configFolder);
            app.ControlledApplication.ApplicationInitialized += OnAppInitialized;
            app.ControlledApplication.DocumentOpened += OnDocumentOpened;
        }

        private static void OnDocumentOpened(object sender, DocumentOpenedEventArgs e)
        {
            var doc = e.Document;
            string key = GetDocKey(doc);
            var (freq, activo) = LeerConfigDesdeArchivo(doc);
            _frecuenciaDocs[key] = freq;
            _activoDocs[key] = activo;
            if (!_ultimoGuardado.ContainsKey(key))
                _ultimoGuardado[key] = DateTime.MinValue;
        }

        private static void OnAppInitialized(object sender, ApplicationInitializedEventArgs e)
        {
            if (sender is not Application appReal) return;
            _uiApp = new UIApplication(appReal);
            _uiApp.Idling += OnIdling;
        }

        private static void OnIdling(object sender, IdlingEventArgs e)
        {
            if (_uiApp == null) return;
            foreach (Document doc in _uiApp.Application.Documents)
            {
                if (doc == null || doc.IsLinked) continue;
                string key = GetDocKey(doc);

                if (!_frecuenciaDocs.ContainsKey(key))
                {
                    var (freq, activo) = LeerConfigDesdeArchivo(doc);
                    _frecuenciaDocs[key] = freq;
                    _activoDocs[key] = activo;
                }

                if (!_activoDocs.ContainsKey(key) || !_activoDocs[key]) continue;
                if (!doc.IsModified) continue;

                if (_guardadoPendiente.ContainsKey(key) && _guardadoPendiente[key])
                {
                    if (_tiempoPendiente.ContainsKey(key) && (DateTime.Now - _tiempoPendiente[key]).TotalSeconds > 30)
                    {
                        try { doc.Save(); } catch { }
                        _guardadoPendiente[key] = false;
                        _ultimoGuardado[key] = DateTime.Now;
                        continue;
                    }

                    try
                    {
                        doc.Save();
                        _ultimoGuardado[key] = DateTime.Now;
                        _guardadoPendiente[key] = false;
                    }
                    catch { }
                }
                else
                {
                    if (!_ultimoGuardado.ContainsKey(key))
                        _ultimoGuardado[key] = DateTime.MinValue;
                    int freq = _frecuenciaDocs[key];
                    if ((DateTime.Now - _ultimoGuardado[key]).TotalMinutes >= freq)
                    {
                        _guardadoPendiente[key] = true;
                        _tiempoPendiente[key] = DateTime.Now;
                    }
                }
            }
        }

        public static void Detener()
        {
            if (_uiApp != null)
                _uiApp.Idling -= OnIdling;
        }

        private static string GetDocKey(Document doc)
        {
            if (!string.IsNullOrEmpty(doc.PathName))
                return Path.GetFileNameWithoutExtension(doc.PathName);
            else
                return doc.Title.GetHashCode().ToString();
        }

        private static string GetConfigPath(Document doc)
        {
            if (string.IsNullOrEmpty(doc.PathName)) return null;
            var name = Path.GetFileNameWithoutExtension(doc.PathName);
            return Path.Combine(_configFolder, $"{name}.json");
        }

        private static (int frecuencia, bool activo) LeerConfigDesdeArchivo(Document doc)
        {
            var configPath = GetConfigPath(doc);
            if (configPath != null && File.Exists(configPath))
            {
                try
                {
                    var json = File.ReadAllText(configPath);
                    var config = JsonSerializer.Deserialize<GuardadoConfig>(json);
                    if (config != null)
                        return (config.FrecuenciaMin, config.Activo);
                }
                catch { }
            }
            return (15, false);
        }

        public static (int frecuencia, bool activo) ObtenerConfig(Document doc)
        {
            string key = GetDocKey(doc);
            if (_frecuenciaDocs.ContainsKey(key))
                return (_frecuenciaDocs[key], _activoDocs[key]);
            return LeerConfigDesdeArchivo(doc);
        }

        public static void GuardarConfig(Document doc, int frecuencia, bool activo)
        {
            var configPath = GetConfigPath(doc);
            if (configPath == null) return;
            var config = new GuardadoConfig { FrecuenciaMin = frecuencia, Activo = activo };
            var json = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(configPath, json);

            string key = GetDocKey(doc);
            _frecuenciaDocs[key] = frecuencia;
            _activoDocs[key] = activo;
            if (!activo) _guardadoPendiente[key] = false;
        }

        public static string FormatearPeso(long bytes)
        {
            if (bytes >= 1073741824) return $"{bytes / 1073741824.0:F2} GB";
            if (bytes >= 1048576) return $"{bytes / 1048576.0:F2} MB";
            return $"{bytes / 1024.0:F2} KB";
        }
    }

    internal class GuardadoConfig
    {
        public int FrecuenciaMin { get; set; } = 15;
        public bool Activo { get; set; } = false;
    }
}