
export function getTabs() {
    return [
        { href: 'configurationpage?name=usermanagement_user', name: 'Groups' },
        { href: 'configurationpage?name=usermanagement_invites', name: 'Invites' },
        { href: 'configurationpage?name=usermanagement_settings', name: 'Settings' }
    ];
}

export function generateGuid() {
    return 'xxxxxxxx-xxxx-4xxx-yxxx-xxxxxxxxxxxx'.replace(/[xy]/g, function (c) {
        var r = Math.random() * 16 | 0, v = c === 'x' ? r : (r & 0x3 | 0x8);
        return v.toString(16);
    });
}

export function createShared(view) {
    return {
        pluginId: '670167bd-e7f8-4549-98e2-5ab2e11bc89f',

        escapeHtml: function(str) {
            if (str === null || str === undefined) return '';
            return String(str)
                .replace(/&/g, '&amp;').replace(/</g, '&lt;')
                .replace(/>/g, '&gt;').replace(/"/g, '&quot;');
        },

        formatDate: function(value) {
            if (!value) return '';
            var d = new Date(value);
            if (isNaN(d.getTime())) return '';
            return d.toLocaleString();
        },

        toDateInput: function(value) {
            if (!value) return '';
            var d = new Date(value);
            if (isNaN(d.getTime())) return '';
            return d.toISOString().slice(0, 10);
        },

        getConfig: function() {
            var self = this;
            return new Promise(function(resolve, reject) {
                ApiClient.getPluginConfiguration(self.pluginId).then(resolve).catch(reject);
            });
        },

        saveConfig: function(config) {
            var self = this;
            return new Promise(function(resolve, reject) {
                ApiClient.updatePluginConfiguration(self.pluginId, config).then(resolve).catch(reject);
            });
        },

        apiRequest: function(endpoint, method, data) {
            var options = {
                url: ApiClient.getUrl('UserManagement/' + endpoint),
                type: method || 'GET',
                dataType: 'json'
            };

            if (data !== undefined && data !== null) {
                options.contentType = 'application/json';
                options.data = JSON.stringify(data);
            }

            return ApiClient.fetch(options).catch(function(error) {
                if (error && error.message && error.message.indexOf('JSON') !== -1) {
                    return null;
                }
                throw error;
            });
        },

        badge: function(cls, label) {
            return '<span class="pt-status-badge ' + cls + '">' + this.escapeHtml(label) + '</span>';
        },

        getEl: function(id) {
            return view.querySelector('#' + id);
        },

        setVisible: function(id, visible) {
            var el = typeof id === 'string' ? view.querySelector('#' + id) : id;
            if (el) {
                if (visible) el.classList.remove('hidden');
                else el.classList.add('hidden');
            }
        },

        setStatus: function(elementId, message, isError) {
            var el = view.querySelector('#' + elementId);
            if (el) {
                el.textContent = message;
                el.style.color = isError ? 'var(--um-error-color)' : 'var(--um-success-color)';
                if (message) {
                    setTimeout(function() { if (el.textContent === message) el.textContent = ''; }, 5000);
                }
            }
        },

        initCollapsibles: function() {
            view.querySelectorAll('.collapsibleHeader').forEach(function(header) {
                if (header.dataset.umBound) return;
                header.dataset.umBound = '1';
                header.addEventListener('click', function() {
                    var targetId = this.dataset.target;
                    var content = view.querySelector('#' + targetId);
                    if (content) {
                        this.classList.toggle('collapsed');
                        content.classList.toggle('collapsed');
                        var isExpanded = !this.classList.contains('collapsed');
                        this.setAttribute('aria-expanded', String(isExpanded));
                    }
                });
            });
        }
    };
}
