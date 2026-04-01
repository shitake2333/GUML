using Microsoft.CodeAnalysis;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;

namespace GUML.LSP;

public class CompletionHandler : CompletionHandlerBase
{
    readonly Workspace _workspace;
    readonly TextDocumentSelector _documentSelector;
    readonly Func<ILanguageServer?> _getServer;
    
    
    public CompletionHandler(Workspace workspace, TextDocumentSelector documentSelector, Func<ILanguageServer?> getServer)
    {
        _workspace = workspace;
        _documentSelector = documentSelector;
        _getServer = getServer;

    }
    
    protected override CompletionRegistrationOptions CreateRegistrationOptions(CompletionCapability capability,
        ClientCapabilities clientCapabilities)
    {
        throw new NotImplementedException();
    }

    public override Task<CompletionList> Handle(CompletionParams request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    public override Task<CompletionItem> Handle(CompletionItem request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}