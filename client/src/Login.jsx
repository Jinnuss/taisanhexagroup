import React, { useState } from "react";

import {
  login
} from "./services/authService";

function Login({ onLogin }) {
  const [email, setEmail] = useState(
    "admin@hexa.local"
  );

  const [password, setPassword] = useState(
    "Admin@123456"
  );

  const [message, setMessage] = useState("");
  const [loading, setLoading] = useState(false);

  async function handleSubmit(event) {
    event.preventDefault();

    try {
      setLoading(true);
      setMessage("");

      const result = await login(email, password);
      onLogin(result.user);
    } catch (error) {
      setMessage(
        error.response?.data?.message ||
        "Không thể đăng nhập."
      );
    } finally {
      setLoading(false);
    }
  }

  return (
    <main className="login-page">
      <form
        className="login-card"
        onSubmit={handleSubmit}
      >
        <p className="eyebrow">HEXA GROUP</p>
        <h1>Đăng nhập</h1>

        <label>
          Email
          <input
            type="email"
            value={email}
            onChange={(event) =>
              setEmail(event.target.value)
            }
            required
          />
        </label>

        <label>
          Mật khẩu
          <input
            type="password"
            value={password}
            onChange={(event) =>
              setPassword(event.target.value)
            }
            required
          />
        </label>

        {message && (
          <p className="error-message">
            {message}
          </p>
        )}

        <button type="submit" disabled={loading}>
          {loading ? "Đang đăng nhập..." : "Đăng nhập"}
        </button>
      </form>
    </main>
  );
}

export default Login;