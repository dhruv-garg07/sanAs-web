using System.Collections.Concurrent;
using Scriban;
using Scriban.Runtime;

namespace SanAsPrime.Services;

public class TemplateRenderer
{
    private readonly string _templatesDirectory;
    private readonly ConcurrentDictionary<string, (Template template, DateTime lastModified)> _cache = new();
    private readonly IWebHostEnvironment _env;

    public TemplateRenderer(IWebHostEnvironment env)
    {
        _env = env;
        if (Directory.Exists(Path.Combine(env.ContentRootPath, "templates")))
        {
            _templatesDirectory = Path.Combine(env.ContentRootPath, "templates");
        }
        else if (Directory.Exists(Path.Combine(AppContext.BaseDirectory, "templates")))
        {
            _templatesDirectory = Path.Combine(AppContext.BaseDirectory, "templates");
        }
        else
        {
            _templatesDirectory = Path.Combine(Directory.GetCurrentDirectory(), "templates");
        }
    }

    public string Render(string templateName, Dictionary<string, object?> model)
    {
        var filePath = Path.Combine(_templatesDirectory, templateName);
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"Template not found: {templateName}", filePath);
        }

        var lastWriteTime = File.GetLastWriteTimeUtc(filePath);

        if (!_cache.TryGetValue(templateName, out var cached) || cached.lastModified < lastWriteTime)
        {
            var content = File.ReadAllText(filePath);
            var template = Template.ParseLiquid(content);
            if (template.HasErrors)
            {
                var errors = string.Join("; ", template.Messages.Select(m => m.ToString()));
                throw new InvalidOperationException($"Error parsing template {templateName}: {errors}");
            }
            cached = (template, lastWriteTime);
            _cache[templateName] = cached;
        }

        var scriptObject = new ScriptObject();
        foreach (var kvp in model)
        {
            scriptObject.Add(kvp.Key, kvp.Value);
        }

        var context = new TemplateContext();
        context.PushGlobal(scriptObject);

        return cached.template.Render(context);
    }
}
