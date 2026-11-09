// Documents are fetched by the server with the admin's token, then streamed here and shown from a
// local "blob:" URL. The browser never needs the token, and the file never gets a public address.
window.samasarathi = {
    async blobUrl(streamRef, contentType) {
        const buffer = await streamRef.arrayBuffer();
        return URL.createObjectURL(new Blob([buffer], { type: contentType }));
    },
    revoke(url) {
        if (url) URL.revokeObjectURL(url);
    },
};
