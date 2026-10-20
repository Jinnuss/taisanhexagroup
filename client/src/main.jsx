import React from "react";
import ReactDOM from "react-dom/client";
import App from "./App.jsx";
import PublicAssetPage from "./PublicAssetPage.jsx";
import "./App.css";

const publicId = new URLSearchParams(window.location.search).get("asset");

ReactDOM.createRoot(document.getElementById("root")).render(
  <React.StrictMode>{publicId ? <PublicAssetPage publicId={publicId} /> : <App />}</React.StrictMode>
);
