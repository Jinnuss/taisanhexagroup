import api from "./api";

export async function login(email, password) {
  const response = await api.post("/auth/login", {
    email,
    password
  });

  localStorage.setItem(
    "accessToken",
    response.data.token
  );

  localStorage.setItem(
    "currentUser",
    JSON.stringify(response.data.user)
  );

  return response.data;
}

export function logout() {
  localStorage.removeItem("accessToken");
  localStorage.removeItem("currentUser");
}

export function getStoredUser() {
  const value = localStorage.getItem("currentUser");

  if (!value) {
    return null;
  }

  try {
    return JSON.parse(value);
  } catch {
    return null;
  }
}
export async function getCurrentUser() {
  const { data } = await api.get("/auth/me");
  localStorage.setItem("currentUser", JSON.stringify(data));
  return data;
}
