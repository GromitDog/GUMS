async function downloadFileFromStream(fileName, contentStreamReference) {
    const arrayBuffer = await contentStreamReference.arrayBuffer();
    const blob = new Blob([arrayBuffer]);
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = fileName;
    a.click();
    URL.revokeObjectURL(url);
}

async function copyTextToClipboard(text) {
    try {
        await navigator.clipboard.writeText(text);
        return true;
    } catch (e) {
        return false;
    }
}
