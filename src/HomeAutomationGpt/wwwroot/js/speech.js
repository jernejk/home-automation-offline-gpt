window.speechToText = {
    start: async function() {
        return new Promise((resolve, reject) => {
            const SpeechRecognition = window.SpeechRecognition || window.webkitSpeechRecognition;
            if (!SpeechRecognition) {
                reject('Speech recognition not supported');
                return;
            }
            const recognition = new SpeechRecognition();
            recognition.lang = 'en-US';
            recognition.interimResults = false;
            recognition.maxAlternatives = 1;
            recognition.onresult = e => {
                const transcript = e.results[0][0].transcript;
                resolve(transcript);
            };
            recognition.onerror = e => reject(e.error);
            recognition.start();
        });
    }
};
