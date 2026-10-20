import React, { useEffect, useState } from "react";
import { createUser, getUsers, resetUserPassword, updateUser } from "./services/userService";
import { getDepartments } from "./services/departmentService";

const emptyForm = {
  employeeCode: "", fullName: "", email: "", password: "",
  role: "employee", departmentId: "", isActive: true
};

export default function UserManager({ currentUser, onBack }) {
  const [users, setUsers] = useState([]);
  const [departments, setDepartments] = useState([]);
  const [filters, setFilters] = useState({ search: "", role: "", active: "", departmentId: "" });
  const [form, setForm] = useState(emptyForm);
  const [editingId, setEditingId] = useState(null);
  const [modalOpen, setModalOpen] = useState(false);
  const [message, setMessage] = useState("");
  const [loading, setLoading] = useState(false);

  useEffect(() => {
    getDepartments().then(setDepartments).catch(showError);
  }, []);

  useEffect(() => {
    const timer = setTimeout(loadUsers, 300);
    return () => clearTimeout(timer);
  }, [filters.search, filters.role, filters.active, filters.departmentId]);

  function showError(error) {
    setMessage(error?.response?.data?.message || "Đã xảy ra lỗi.");
  }

  async function loadUsers() {
    try {
      setLoading(true);
      setUsers(await getUsers({
        search: filters.search || undefined,
        role: filters.role || undefined,
        active: filters.active || undefined,
        departmentId: filters.departmentId || undefined
      }));
    } catch (error) {
      showError(error);
    } finally {
      setLoading(false);
    }
  }

  function openCreate() {
    setEditingId(null);
    setForm(emptyForm);
    setMessage("");
    setModalOpen(true);
  }

  function openEdit(user) {
    setEditingId(user.Id);
    setForm({
      employeeCode: user.EmployeeCode || "", fullName: user.FullName || "",
      email: user.Email || "", password: "", role: user.Role || "employee",
      departmentId: user.DepartmentId == null ? "" : String(user.DepartmentId),
      isActive: Boolean(user.IsActive)
    });
    setMessage("");
    setModalOpen(true);
  }

  async function submit(event) {
    event.preventDefault();
    try {
      const data = {
        ...form,
        departmentId: form.departmentId ? Number(form.departmentId) : null
      };
      if (editingId) await updateUser(editingId, data);
      else await createUser(data);
      setModalOpen(false);
      setMessage(editingId ? "Cập nhật tài khoản thành công." : "Tạo tài khoản thành công.");
      await loadUsers();
    } catch (error) {
      showError(error);
    }
  }

  async function toggleActive(user) {
    const action = user.IsActive ? "khóa" : "mở khóa";
    if (!window.confirm(`Bạn muốn ${action} tài khoản ${user.FullName}?`)) return;
    try {
      await updateUser(user.Id, {
        employeeCode: user.EmployeeCode, fullName: user.FullName, email: user.Email,
        role: user.Role, departmentId: user.DepartmentId, isActive: !user.IsActive
      });
      setMessage(`${action[0].toUpperCase() + action.slice(1)} tài khoản thành công.`);
      await loadUsers();
    } catch (error) {
      showError(error);
    }
  }

  async function resetPassword(user) {
    const password = window.prompt(`Nhập mật khẩu mới cho ${user.FullName}:\nTối thiểu 8 ký tự, có hoa, thường, số và ký tự đặc biệt.`);
    if (!password) return;
    try {
      const result = await resetUserPassword(user.Id, password);
      setMessage(result.message);
    } catch (error) {
      showError(error);
    }
  }

  return (
    <main className="app-shell">
      <header className="topbar">
        <div><p className="eyebrow">QUẢN TRỊ HỆ THỐNG</p><h1>Quản lý tài khoản</h1></div>
        <button className="back-btn" onClick={onBack}>← Quay lại tài sản</button>
      </header>

      <section className="filter-panel user-filter">
        <div className="filters">
          <div className="search-box"><span>⌕</span><input value={filters.search} onChange={(e) => setFilters({ ...filters, search: e.target.value })} placeholder="Tìm mã nhân viên, họ tên, email..." /></div>
          <select value={filters.departmentId} onChange={(e) => setFilters({ ...filters, departmentId: e.target.value })}><option value="">Tất cả công ty</option>{departments.map((x) => <option key={x.Id} value={x.Id}>{x.DepartmentName}</option>)}</select>
          <select value={filters.role} onChange={(e) => setFilters({ ...filters, role: e.target.value })}><option value="">Tất cả vai trò</option><option value="admin">Quản trị viên</option><option value="employee">Nhân viên</option></select>
          <select value={filters.active} onChange={(e) => setFilters({ ...filters, active: e.target.value })}><option value="">Tất cả trạng thái</option><option value="true">Đang hoạt động</option><option value="false">Đã khóa</option></select>
          <button className="primary-btn" onClick={openCreate}>+ Tạo tài khoản</button>
        </div>
      </section>

      {message && <div className="notice"><span>{message}</span><button onClick={() => setMessage("")}>×</button></div>}

      <section className="table-card">
        <div className="table-title"><div><h2>Danh sách người dùng</h2><p>{users.length} tài khoản</p></div><button onClick={loadUsers}>↻ Làm mới</button></div>
        <div className="table-wrap">
          <table><thead><tr><th>Mã nhân viên</th><th>Họ tên</th><th>Email</th><th>Công ty</th><th>Vai trò</th><th>Trạng thái</th><th>Thao tác</th></tr></thead>
          <tbody>{loading ? <tr><td colSpan="7" className="empty-row">Đang tải...</td></tr> : users.length === 0 ? <tr><td colSpan="7" className="empty-row">Chưa có tài khoản.</td></tr> : users.map((user) => (
            <tr key={user.Id}>
              <td className="asset-code">{user.EmployeeCode || "—"}</td><td>{user.FullName}</td><td>{user.Email}</td>
              <td><span className="company-badge">{user.DepartmentCode || user.DepartmentName || "Chưa gán"}</span></td>
              <td><span className={`role-badge ${user.Role}`}>{user.Role === "admin" ? "Quản trị viên" : "Nhân viên"}</span></td>
              <td><span className={`account-status ${user.IsActive ? "active" : "locked"}`}>● {user.IsActive ? "Hoạt động" : "Đã khóa"}</span></td>
              <td><div className="user-actions"><button onClick={() => openEdit(user)}>Sửa</button><button onClick={() => resetPassword(user)}>Đặt mật khẩu</button><button className={user.IsActive ? "lock-btn" : "unlock-btn"} disabled={user.Id === currentUser.id} onClick={() => toggleActive(user)}>{user.IsActive ? "Khóa" : "Mở khóa"}</button></div></td>
            </tr>
          ))}</tbody></table>
        </div>
      </section>

      {modalOpen && <div className="modal-overlay" onMouseDown={(e) => e.target === e.currentTarget && setModalOpen(false)}>
        <div className="asset-modal user-modal">
          <div className="modal-header"><div><p className="eyebrow">TÀI KHOẢN</p><h2>{editingId ? "Cập nhật tài khoản" : "Tạo tài khoản mới"}</h2></div><button className="modal-close" onClick={() => setModalOpen(false)}>×</button></div>
          <form onSubmit={submit}><div className="modal-form-grid">
            <label>Mã nhân viên<input value={form.employeeCode} onChange={(e) => setForm({ ...form, employeeCode: e.target.value })} placeholder="NV001" /></label>
            <label>Họ và tên<input value={form.fullName} onChange={(e) => setForm({ ...form, fullName: e.target.value })} required /></label>
            <label className="wide">Email đăng nhập<input type="email" value={form.email} onChange={(e) => setForm({ ...form, email: e.target.value })} required /></label>
            {!editingId && <label className="wide">Mật khẩu ban đầu<input type="password" value={form.password} onChange={(e) => setForm({ ...form, password: e.target.value })} required placeholder="Tối thiểu 8 ký tự, hoa, thường, số, ký tự đặc biệt" /></label>}
            <label>Công ty<select value={form.departmentId} onChange={(e) => setForm({ ...form, departmentId: e.target.value })}><option value="">-- Chưa gán --</option>{departments.map((x) => <option key={x.Id} value={x.Id}>{x.DepartmentName}</option>)}</select></label>
            <label>Vai trò<select value={form.role} onChange={(e) => setForm({ ...form, role: e.target.value })}><option value="employee">Nhân viên</option><option value="admin">Quản trị viên</option></select></label>
            {editingId && <label className="checkbox-label wide"><input type="checkbox" checked={form.isActive} onChange={(e) => setForm({ ...form, isActive: e.target.checked })} /> Tài khoản đang hoạt động</label>}
          </div><div className="modal-footer"><button type="button" className="cancel-btn" onClick={() => setModalOpen(false)}>Hủy</button><button type="submit" className="primary-btn">{editingId ? "Lưu thay đổi" : "Tạo tài khoản"}</button></div></form>
        </div>
      </div>}
    </main>
  );
}
