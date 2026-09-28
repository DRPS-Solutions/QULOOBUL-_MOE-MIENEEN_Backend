// Browser speech-to-text for the meeting minutes. Returns false when unsupported (e.g. Firefox).
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
