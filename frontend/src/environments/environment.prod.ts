// Em produção (Docker), o nginx faz proxy das chamadas.
// /api/estoque/ → estoque-api:8080/api/
// /api/faturamento/ → faturamento-api:8080/api/
// Assim o frontend não precisa saber o endereço interno dos containers.
export const environment = {
  production: true,
  estoqueApiUrl: '',      // relativo: usa o proxy nginx
  faturamentoApiUrl: ''   // relativo: usa o proxy nginx
};
