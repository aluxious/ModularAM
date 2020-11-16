import dash
import dash_core_components as dcc
import dash_html_components as html
import plotly.graph_objs as go
import pyodbc

server = 'tcp:modularam.database.windows.net'
database = 'mamDb'
username = 'aluxious'
password = 'Qaz1wsx2@'
driver= '{ODBC Driver 17 for SQL Server}'


ISINS = [ {'label': '', 'value': ''} ]

with pyodbc.connect('DRIVER='+driver+';SERVER='+server+';PORT=1433;DATABASE='+database+';UID='+username+';PWD='+ password) as conn:
    with conn.cursor() as cursor:
        cursor.execute("select DISTINCT(isin) from secwise_holdings ORDER BY isin")
        row = cursor.fetchone()
        while row:
            ISINS.append({'label': '' + str(row[0]) + '', 'value': '' + str(row[0]) + ''})
            row = cursor.fetchone()

external_stylesheets = ['https://codepen.io/chriddyp/pen/bWLwgP.css']

dash_app = dash.Dash(__name__, external_stylesheets=external_stylesheets)
app = dash_app.server

dash_app.layout = html.Div(children=[
    html.H1(children='Security Wise Holdings'),

    html.H4(children='Daily Indicative Value Of Aggregate Holding of FPIS (INR CR#)'),

    html.Table(children=[
        html.Tr(children=[
            html.Td(
                html.Label('Select ISIN: ')
            ),
            html.Td(children=[
                dcc.Dropdown(
                    id='isins-dropdown',
                    options=ISINS,
                    value=''
                )
            ], style={'width': 200}),
        ]),
    ]),
    html.Div(id='dd-output-container')
])

@dash_app.callback(
    dash.dependencies.Output('dd-output-container', 'children'),
    [dash.dependencies.Input('isins-dropdown', 'value')])

def update_output(value):
    GRAPHDATA_X = [ ]
    GRAPHDATA_Y = [ ]
    
    with pyodbc.connect('DRIVER='+driver+';SERVER='+server+';PORT=1433;DATABASE='+database+';UID='+username+';PWD='+ password) as conn:
        with conn.cursor() as cursor:
            cursor.execute("select parse(cutoff_date as date) as [date], parse(indicative_value as decimal(18,7)) as [value] from secwise_holdings where isin='"+str(value)+"' order by [date]")
            row = cursor.fetchone()
            while row:
                GRAPHDATA_X.append(row[0])
                GRAPHDATA_Y.append(row[1])
                row = cursor.fetchone()
    
    fig = go.Figure(data=[go.Scatter(x=GRAPHDATA_X, y=GRAPHDATA_Y)])

    return dcc.Graph(
        id='SecWisePlot',
        figure=fig
    )

if __name__ == '__main__':
    dash_app.run_server(debug=True)