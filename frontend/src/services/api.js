import axios from 'axios'

const http = axios.create({ baseURL: import.meta.env.VITE_API_URL ?? 'http://localhost:5000' })

export const notificar = (mensaje) => alert(mensaje)

// Interceptor global: cualquier error HTTP se informa de forma uniforme, sin tener que
// invocarlo en cada catch. Distingue sin conexión / validación o regla de negocio / error del servidor.
http.interceptors.response.use(
  (response) => response,
  (error) => {
    const status = error.response?.status
    let mensaje
    if (!error.response) mensaje = 'No se pudo conectar con el servidor. Verificá que el backend esté levantado.'
    else if (status >= 500) mensaje = 'Error del servidor. Intentá nuevamente en unos instantes.'
    else mensaje = error.response.data?.mensaje || 'La solicitud no pudo procesarse.'

    error.mensaje = mensaje
    error.tipo = !error.response ? 'red' : status >= 500 ? 'servidor' : 'negocio'
    notificar(mensaje)
    return Promise.reject(error)
  }
)

export const consultasApi = {
  getAll:             ()          => http.get('/consultas'),
  getById:            (id)        => http.get(`/consultas/${id}`),
  create:             (data)      => http.post('/consultas', data),
  cancelar:           (id)        => http.post(`/consultas/${id}/cancelar`),
  marcarInasistencia: (id)        => http.post(`/consultas/${id}/inasistencia`),
  registrarAtencion:  (id)        => http.post(`/consultas/${id}/atencion`),
  actualizarEstado:   (id, data)  => http.put(`/consultas/${id}/estado`, data)
}

export const afiliadosApi = {
  getAll:  ()          => http.get('/afiliados'),
  getById: (id)        => http.get(`/afiliados/${id}`),
  create:  (data)      => http.post('/afiliados', data),
  update:  (id, data)  => http.put(`/afiliados/${id}`, data),
  delete:  (id)        => http.delete(`/afiliados/${id}`)
}

export const profesionalesApi = {
  getAll: () => http.get('/profesionales')
}

export const sedesApi = {
  getAll: () => http.get('/sedes')
}
