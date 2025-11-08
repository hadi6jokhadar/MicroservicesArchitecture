// Configuration
let hubConnections = {
  user1: null,
  user2: null,
};

// DOM Elements
const elements = {
  // User 1 Section
  user1Token: document.getElementById("user1-token"),
  user1TenantId: document.getElementById("user1-tenant-id"),
  btnConnect1: document.getElementById("btn-connect-1"),
  btnDisconnect1: document.getElementById("btn-disconnect-1"),
  btnClear1: document.getElementById("btn-clear-1"),
  notifications1: document.getElementById("notifications-1"),
  connectionStatus1: document.getElementById("connection-status-1"),

  // User 2 Section
  user2Token: document.getElementById("user2-token"),
  user2TenantId: document.getElementById("user2-tenant-id"),
  btnConnect2: document.getElementById("btn-connect-2"),
  btnDisconnect2: document.getElementById("btn-disconnect-2"),
  btnClear2: document.getElementById("btn-clear-2"),
  notifications2: document.getElementById("notifications-2"),
  connectionStatus2: document.getElementById("connection-status-2"),

  // Configuration
  hubUrl: document.getElementById("hub-url"),
  apiUrl: document.getElementById("api-url"),
  autoReconnect: document.getElementById("auto-reconnect"),
};

// Utility Functions
function updateConnectionStatus(user, status) {
  const statusElement =
    user === 1 ? elements.connectionStatus1 : elements.connectionStatus2;
  statusElement.textContent = status.charAt(0).toUpperCase() + status.slice(1);
  statusElement.className = `connection-status ${status}`;
}

function addNotification(user, notification) {
  const container =
    user === 1 ? elements.notifications1 : elements.notifications2;

  const notificationElement = document.createElement("div");
  notificationElement.className = "notification-item";

  const time = new Date().toLocaleTimeString();

  notificationElement.innerHTML = `
        <div class="notification-header">
            <div class="notification-title">${escapeHtml(
              notification.title || "No Title"
            )}</div>
            <div class="notification-time">⏰ ${time}</div>
        </div>
        <div class="notification-message">${escapeHtml(
          notification.message || "No Message"
        )}</div>
        <div class="notification-metadata">
            ${
              notification.tenantId
                ? `
                <div class="notification-metadata-item">
                    <span class="notification-metadata-label">Tenant ID</span>
                    <span class="notification-metadata-value">${escapeHtml(
                      notification.tenantId
                    )}</span>
                </div>
            `
                : ""
            }
            ${
              notification.userId
                ? `
                <div class="notification-metadata-item">
                    <span class="notification-metadata-label">User ID</span>
                    <span class="notification-metadata-value">${notification.userId}</span>
                </div>
            `
                : ""
            }
            ${
              notification.priority !== undefined
                ? `
                <div class="notification-metadata-item">
                    <span class="notification-metadata-label">Priority</span>
                    <span class="notification-metadata-value">${getPriorityName(
                      notification.priority
                    )}</span>
                </div>
            `
                : ""
            }
            ${
              notification.queueItemId
                ? `
                <div class="notification-metadata-item">
                    <span class="notification-metadata-label">Queue ID</span>
                    <span class="notification-metadata-value">${notification.queueItemId}</span>
                </div>
            `
                : ""
            }
        </div>
    `;

  container.insertBefore(notificationElement, container.firstChild);

  // Auto-scroll to top
  container.scrollTop = 0;

  // Show browser notification if permitted
  if (Notification.permission === "granted") {
    new Notification(notification.title || "New Notification", {
      body: notification.message || "",
      icon: "🔔",
    });
  }
}

function escapeHtml(text) {
  const div = document.createElement("div");
  div.textContent = text;
  return div.innerHTML;
}

function getPriorityName(priority) {
  const priorities = ["Immediate", "Waitable", "Background"];
  return priorities[priority] || "Unknown";
}

function getDeliveryTypeName(deliveryType) {
  const types = ["SignalR", "Firebase", "Both"];
  return types[deliveryType] || "Unknown";
}

// SignalR Connection Functions
async function connectUser(userNumber) {
  const token =
    userNumber === 1
      ? elements.user1Token.value.trim()
      : elements.user2Token.value.trim();
  const tenantId =
    userNumber === 1
      ? elements.user1TenantId.value.trim()
      : elements.user2TenantId.value.trim();
  const hubUrl = elements.hubUrl.value.trim();

  if (!hubUrl) {
    alert("Hub URL is required");
    return;
  }

  try {
    updateConnectionStatus(userNumber, "connecting");

    // Build hub URL with tenant ID in query string if provided
    let finalHubUrl = hubUrl;
    if (tenantId) {
      const separator = hubUrl.includes("?") ? "&" : "?";
      finalHubUrl = `${hubUrl}${separator}tenantId=${encodeURIComponent(
        tenantId
      )}`;
    }

    console.log(`User ${userNumber} connecting to: ${finalHubUrl}`);

    // Build connection options
    const connectionOptions = {
      skipNegotiation: false,
      transport:
        signalR.HttpTransportType.WebSockets |
        signalR.HttpTransportType.ServerSentEvents |
        signalR.HttpTransportType.LongPolling,
    };

    // Add token if provided
    if (token) {
      connectionOptions.accessTokenFactory = () => token;
    }

    // Add custom headers for tenant ID (fallback, though query string is primary)
    if (tenantId) {
      connectionOptions.headers = { "x-tenant-id": tenantId };
    }

    const connectionBuilder = new signalR.HubConnectionBuilder()
      .withUrl(finalHubUrl, connectionOptions)
      .configureLogging(signalR.LogLevel.Information);

    if (elements.autoReconnect.checked) {
      connectionBuilder.withAutomaticReconnect();
    }

    const connection = connectionBuilder.build();

    // Event handlers
    connection.on("ReceiveNotification", (notification) => {
      console.log(`User ${userNumber} received notification:`, notification);
      addNotification(userNumber, notification);

      // Acknowledge delivery if authenticated and queueItemId is present
      if (token && notification.queueItemId) {
        connection
          .invoke("AcknowledgeDelivery", notification.queueItemId)
          .then(() => {
            console.log(
              `User ${userNumber} acknowledged notification ${notification.queueItemId}`
            );
          })
          .catch((err) => {
            console.error(`User ${userNumber} failed to acknowledge:`, err);
          });
      }
    });

    connection.onclose((error) => {
      console.log(`User ${userNumber} disconnected`, error);
      updateConnectionStatus(userNumber, "disconnected");
      toggleButtons(userNumber, false);
    });

    connection.onreconnecting((error) => {
      console.log(`User ${userNumber} reconnecting...`, error);
      updateConnectionStatus(userNumber, "connecting");
    });

    connection.onreconnected((connectionId) => {
      console.log(`User ${userNumber} reconnected:`, connectionId);
      updateConnectionStatus(userNumber, "connected");
    });

    await connection.start();

    const key = userNumber === 1 ? "user1" : "user2";
    hubConnections[key] = connection;

    updateConnectionStatus(userNumber, "connected");
    toggleButtons(userNumber, true);

    const authType = token ? "Authenticated" : "Anonymous";
    const tenantMsg = tenantId ? ` (Tenant: ${tenantId})` : " (Global only)";
    console.log(
      `User ${userNumber} connected successfully! ${authType}${tenantMsg}`
    );
  } catch (error) {
    console.error(`User ${userNumber} connection failed:`, error);
    updateConnectionStatus(userNumber, "disconnected");

    // Provide helpful error messages
    let errorMessage = `Connection failed: ${error.message}\n\n`;

    if (
      error.message.includes("Failed to fetch") ||
      error.message.includes("Failed to complete negotiation")
    ) {
      errorMessage += "🔧 Troubleshooting Steps:\n\n";
      errorMessage += "1. Accept SSL Certificate:\n";
      errorMessage += `   Visit: ${hubUrl.replace(
        "/hubs/notifications",
        "/health"
      )}\n`;
      errorMessage += '   Click "Advanced" → "Proceed to localhost"\n\n';
      errorMessage += "2. Serve HTML via HTTP server (NOT file://):\n";
      errorMessage += "   In terminal: python -m http.server 8080\n";
      errorMessage += "   Then open: http://localhost:8080/hub-test.html\n\n";
      errorMessage += "3. Or use HTTP instead of HTTPS:\n";
      errorMessage +=
        "   Change Hub URL to: http://localhost:5004/hubs/notifications\n\n";
      errorMessage += "Check console (F12) for detailed errors.";
    }

    alert(errorMessage);
  }
}

async function disconnectUser(userNumber) {
  const key = userNumber === 1 ? "user1" : "user2";
  const connection = hubConnections[key];

  if (connection) {
    try {
      await connection.stop();
      hubConnections[key] = null;
      updateConnectionStatus(userNumber, "disconnected");
      toggleButtons(userNumber, false);
      console.log(`User ${userNumber} disconnected successfully`);
    } catch (error) {
      console.error(`User ${userNumber} disconnect failed:`, error);
    }
  }
}

function toggleButtons(userNumber, connected) {
  if (userNumber === 1) {
    elements.btnConnect1.disabled = connected;
    elements.btnDisconnect1.disabled = !connected;
  } else {
    elements.btnConnect2.disabled = connected;
    elements.btnDisconnect2.disabled = !connected;
  }
}

function clearNotifications(userNumber) {
  const container =
    userNumber === 1 ? elements.notifications1 : elements.notifications2;
  container.innerHTML = "";
}

// Event Listeners
elements.btnConnect1.addEventListener("click", () => connectUser(1));
elements.btnDisconnect1.addEventListener("click", () => disconnectUser(1));
elements.btnClear1.addEventListener("click", () => clearNotifications(1));

elements.btnConnect2.addEventListener("click", () => connectUser(2));
elements.btnDisconnect2.addEventListener("click", () => disconnectUser(2));
elements.btnClear2.addEventListener("click", () => clearNotifications(2));

// Request notification permission on page load
if ("Notification" in window && Notification.permission === "default") {
  Notification.requestPermission();
}

// Initialize
console.log("Notification Hub Test Page loaded successfully");
console.log("SignalR version:", signalR.VERSION);

// Display page protocol for diagnostics
const pageProtocol = window.location.protocol;
const pageProtocolElement = document.getElementById("page-protocol");
if (pageProtocolElement) {
  if (pageProtocol === "file:") {
    pageProtocolElement.innerHTML = `<span style="color: #dc2626; font-weight: bold;">⚠️ file:// (WILL NOT WORK)</span><br>
            <span style="font-size: 0.85rem;">Serve via HTTP: <code>python -m http.server 8080</code></span>`;
  } else if (pageProtocol === "http:") {
    pageProtocolElement.innerHTML = `<span style="color: #10b981; font-weight: bold;">✅ http:// (Good)</span>`;
  } else if (pageProtocol === "https:") {
    pageProtocolElement.innerHTML = `<span style="color: #10b981; font-weight: bold;">✅ https:// (Good)</span>`;
  } else {
    pageProtocolElement.innerHTML = `<span style="color: #f59e0b;">${pageProtocol}</span>`;
  }
}

// Warn if using file:// protocol
if (pageProtocol === "file:") {
  console.warn(
    "⚠️ WARNING: Page opened as file://. SignalR connections will likely fail due to CORS restrictions."
  );
  console.warn("📝 Solution: Serve via HTTP server");
  console.warn("   Command: python -m http.server 8080");
  console.warn("   Then open: http://localhost:8080/hub-test.html");
}

// Auto-save form data to localStorage
function saveFormData() {
  const formData = {
    hubUrl: elements.hubUrl.value,
    apiUrl: elements.apiUrl.value,
    autoReconnect: elements.autoReconnect.checked,
    user1TenantId: elements.user1TenantId.value,
    user2TenantId: elements.user2TenantId.value,
  };
  localStorage.setItem("notificationHubTestData", JSON.stringify(formData));
}

function loadFormData() {
  const savedData = localStorage.getItem("notificationHubTestData");
  if (savedData) {
    try {
      const formData = JSON.parse(savedData);
      elements.hubUrl.value = formData.hubUrl || elements.hubUrl.value;
      elements.apiUrl.value = formData.apiUrl || elements.apiUrl.value;
      elements.autoReconnect.checked =
        formData.autoReconnect !== undefined ? formData.autoReconnect : true;
      elements.user1TenantId.value = formData.user1TenantId || "";
      elements.user2TenantId.value = formData.user2TenantId || "";
    } catch (e) {
      console.error("Failed to load saved form data:", e);
    }
  }
}

// Load saved data on page load
loadFormData();

// Save data when inputs change
[
  elements.hubUrl,
  elements.apiUrl,
  elements.autoReconnect,
  elements.user1TenantId,
  elements.user2TenantId,
].forEach((element) => {
  element.addEventListener("change", saveFormData);
});

// Clean up connections on page unload
window.addEventListener("beforeunload", () => {
  Object.values(hubConnections).forEach((connection) => {
    if (connection) {
      connection.stop();
    }
  });
});

console.log("🎉 Notification Hub Test Page ready!");
console.log("💡 Tips:");
console.log("  - Browser notifications will appear if permission is granted");
console.log("  - Form data is auto-saved to localStorage");
