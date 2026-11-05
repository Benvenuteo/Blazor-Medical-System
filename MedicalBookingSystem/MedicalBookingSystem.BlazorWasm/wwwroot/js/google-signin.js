// Google Identity Services bridge. The script is loaded only on pages that show the button,
// and only when a client ID is configured. The ID token goes straight to our API for verification.
window.medicalGoogle = {
  loading: null,
  load() {
    if (window.google && window.google.accounts && window.google.accounts.id) return Promise.resolve();
    if (!this.loading) {
      this.loading = new Promise((resolve, reject) => {
        const script = document.createElement('script');
        script.src = 'https://accounts.google.com/gsi/client';
        script.async = true;
        script.onload = resolve;
        script.onerror = () => { this.loading = null; reject(new Error('Google Identity Services did not load.')); };
        document.head.appendChild(script);
      });
    }
    return this.loading;
  },
  async render(element, clientId, nonce, component) {
    await this.load();
    window.google.accounts.id.initialize({
      client_id: clientId,
      nonce: nonce,
      ux_mode: 'popup',
      callback: response => component.invokeMethodAsync('ReceiveCredential', response.credential)
    });
    window.google.accounts.id.renderButton(element, {
      theme: 'outline', size: 'large', text: 'continue_with', shape: 'pill', locale: 'pl', width: 280
    });
  }
};
