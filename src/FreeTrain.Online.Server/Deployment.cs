using System.Net;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.StaticFiles;

namespace FreeTrain.Online.Server;

public static class Deployment
{
    public static void UseDeployment(this WebApplication app)
    {
        // Only an explicitly configured adjacent proxy may supply the TLS scheme.
        // Host stays the original Host header; do not trust arbitrary forwarded hosts.
        if (app.Configuration["FreeTrainOnline:TrustedProxy"] is { Length: > 0 } address)
        {
            var options = new ForwardedHeadersOptions
            {
                ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto,
                ForwardLimit = 1,
            };
            options.KnownProxies.Clear(); options.KnownIPNetworks.Clear();
            options.KnownProxies.Add(IPAddress.Parse(address));
            app.UseForwardedHeaders(options);
        }
        app.UseDefaultFiles();
        var contentTypes = new FileExtensionContentTypeProvider();
        contentTypes.Mappings[".sha256"] = "text/plain";
        app.UseStaticFiles(new StaticFileOptions
        {
            ContentTypeProvider = contentTypes,
            OnPrepareResponse = context =>
            {
                // Vite names code bundles by content hash. Other assets may change in place.
                var name = context.File.Name;
                context.Context.Response.Headers.CacheControl =
                    System.Text.RegularExpressions.Regex.IsMatch(name, @"-[\w-]{8}\.((js)|(css))$")
                    ? "public,max-age=31536000,immutable" : "no-cache";
            },
        });
    }
}
