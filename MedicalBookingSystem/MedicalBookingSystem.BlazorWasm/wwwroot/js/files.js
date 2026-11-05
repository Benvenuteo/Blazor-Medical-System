// Saves text received from the API (authorized request) as a file, e.g. a calendar invitation.
window.medicalFiles = {
  download(fileName, contentType, content) {
    const url = URL.createObjectURL(new Blob([content], { type: contentType }));
    const link = document.createElement('a');
    link.href = url;
    link.download = fileName;
    document.body.appendChild(link);
    link.click();
    link.remove();
    setTimeout(() => URL.revokeObjectURL(url), 1000);
  }
};
