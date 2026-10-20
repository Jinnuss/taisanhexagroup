import api from "./api";

export async function getUsers(params = {}) {
  const response = await api.get("/users", { params });
  return response.data;
}

export async function createUser(data) {
  const response = await api.post("/users", data);
  return response.data;
}

export async function updateUser(id, data) {
  const response = await api.put(`/users/${id}`, data);
  return response.data;
}

export async function resetUserPassword(id, password) {
  const response = await api.post(`/users/${id}/reset-password`, { password });
  return response.data;
}
