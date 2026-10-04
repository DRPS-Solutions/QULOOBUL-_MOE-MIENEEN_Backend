// Browser speech-to-text for the meeting minutes. Returns false when unsupported (e.g. Firefox).
// Reference: MDN Web Docs, "SpeechRecognition." Accessed: Oct. 04, 2026. [Online]. Available: https://developer.mozilla.org/en-US/docs/Web/API/SpeechRecognition
// Reference: Microsoft, "Call .NET methods from JavaScript functions in ASP.NET Core Blazor." Accessed: Oct. 04, 2026. [Online]. Available: https://learn.microsoft.com/en-us/aspnet/core/blazor/javascript-interoperability/call-dotnet-from-javascript
let rec;
window.dictation = {
    start(ref) {
        const SR = window.SpeechRecognition || window.webkitSpeechRecognition;
        if (!SR) return false;
        rec = new SR();
        rec.continuous = true;
        rec.lang = navigator.language || "en-ZA";
        rec.onresult = e => {
            for (let i = e.resultIndex; i < e.results.length; i++)
                if (e.results[i].isFinal) ref.invokeMethodAsync("OnSpeech", e.results[i][0].transcript.trim());
        };
        rec.onend = () => ref.invokeMethodAsync("OnDictationEnded");
        rec.start();
        return true;
    },
    stop() { rec?.stop(); }
};
